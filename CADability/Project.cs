using CADability.Attribute;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.Substitutes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
// using System.Runtime.Serialization.Formatters.Soap; Müsste man mal einführen, referenz muss verwendet werden


namespace CADability
{

    public class ProjectException : ApplicationException
    {
        public enum tExceptionType { InvalidModelIndex };
        public tExceptionType ExceptionType;
        public ProjectException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    public class ProjectSerializationException : SerializationException
    {
        private string resourceId; // resourceId für die MessageBox
        public ProjectSerializationException(string resourceId)
            : base()
        {
            this.resourceId = resourceId;
        }
        public string ResourceId
        {
            get { return resourceId; }
        }
    }
    internal class ProjectOldVersionException : ApplicationException
    {
        private string resourceId; // resourceId für die MessageBox
        public ProjectOldVersionException(string message, Exception innerEx)
            : base(message, innerEx)
        {
        }
    }

    [Serializable]
    internal class UnDeseriazableBlock : Block
    {
        public UnDeseriazableBlock()
        {
        }
        #region ISerializable Members
        protected UnDeseriazableBlock(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                this.UserData.Add(e.Name, e.Value);
            }
        }
        #endregion

    }
    [Serializable]
    internal class UnDeseriazableObject : ISerializable, ISerializationSurrogate
    {
        public UnDeseriazableObject()
        {
        }
        Dictionary<string, object> deserializedObjects;
        #region ISerializable Members
        protected UnDeseriazableObject(SerializationInfo info, StreamingContext context)
        {
            deserializedObjects = new Dictionary<string, object>();
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                deserializedObjects[e.Name] = e.Value;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // es macht keinen Sinn dieses Objekt abzuspeichern, denn der Typname zum späteren deserialisieren
            // ist nicht mehr da.
        }
        #endregion

        #region ISerializationSurrogate Members

        void ISerializationSurrogate.GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }

        object ISerializationSurrogate.SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    sealed class CompareInfoSerializationSurrogate : ISerializationSurrogate
    {
        // hierher kommt die Lösung: http://social.msdn.microsoft.com/Forums/en-US/wcf/thread/283e646b-53d8-4f1d-b677-85d336392fcd
        public void GetObjectData(Object obj,
           SerializationInfo info, StreamingContext context)
        {

            CompareInfo c = (CompareInfo)obj;
        }

        public object SetObjectData(object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector)
        {
            CompareInfo template = CompareInfo.GetCompareInfo((int)info.GetInt32("culture"));
            foreach (var field in typeof(CompareInfo).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                // System.Diagnostics.Trace.WriteLine("field: " + field.ToString());
                field.SetValue(obj, field.GetValue(template));
            }
            return obj;
        }
    }

    class DeserialisationSurrogat : ISurrogateSelector
    {
        #region ISurrogateSelector Members

        void ISurrogateSelector.ChainSelector(ISurrogateSelector selector)
        {
            throw new NotImplementedException();
        }

        ISurrogateSelector ISurrogateSelector.GetNextSelector()
        {
            throw new NotImplementedException();
        }

        ISerializationSurrogate ISurrogateSelector.GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    [Serializable]
    class SCompareInfo : ISerializable
    {
        protected SCompareInfo(SerializationInfo info, StreamingContext context)
        {
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotImplementedException();
        }
        public static implicit operator CompareInfo(SCompareInfo s)
        {
            return CultureInfo.InvariantCulture.CompareInfo;
        }
    }
    /// <summary>
    /// A Project is the database CADability works on. A Project is serializable and contains
    /// one or more Models (which contain GeoObjects) lists of attributes, views and UserData.
    /// A Project is usually saved in a file.
    /// </summary>
    [Serializable]
    [JsonVersion(1)]
    public class Project : IShowPropertyImpl, ISerializable, IAttributeListContainer,
            ICommandHandler, IEnumerable, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        #region polymorph construction
        public delegate Project ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Project Construct()
        {
            if (Constructor != null) return Constructor();
            return new Project();
        }
        #endregion
        private ArrayList models; // die zugehörigen Modelle
        // SingleDocumentFrame ordnet jedem layout ein LayoutView und jedem projectedModel ein ModelView zu
        // Die Views existieren also nur zur Laufzeit, die Layouts und projectedModels sind persistent
        private List<ProjectedModel> projectedModels; // die projectedModels
        private List<AnimatedView> animatedViews; // die AnimatedViews
#if !WEBASSEMBLY
        private List<GDI2DView> gdiViews; // die AnimatedViews
        private List<Layout> layouts; // die zugehörigen Layouts
#endif
        // für jedes Layout gibt es genau einen View, mehr macht ja keinen Sinn
        // für ein Modell allerdings kann es mehrere Projektionen bzw. Ansichten geben,
        // die hier in einer Liste gespeichert werden
        // Name, welches Modell, Projektion, sichtbare Layer
        // vor dem Abspeichern müssen die sichtbaren Layer überprüft werden
        // gibts nur noch um alte dateien zu lesen:
        [Serializable]
        internal class ModelViewDescription : ISerializable
        {
            public string Name;
            public Model Model;
            public Projection Projection;
            public ArrayList VisibleLayers;
            public ModelView modelView;
            public Project project;
            public bool OnlyThinLines;
            public ModelViewDescription(Project project)
            {
                this.project = project;
                VisibleLayers = new ArrayList();
                modelView = null;
            }
            #region ISerializable Members
            public ModelViewDescription(SerializationInfo info, StreamingContext context)
            {
                Name = (string)info.GetValue("Name", typeof(string));
                Model = (Model)info.GetValue("Model", typeof(Model));
                Projection = (Projection)info.GetValue("Projection", typeof(Projection));
                try
                {
                    VisibleLayers = (ArrayList)info.GetValue("VisibleLayers", typeof(ArrayList));
                }
                catch (SerializationException)
                {
                    VisibleLayers = null;
                }
                try
                {
                    OnlyThinLines = (bool)info.GetValue("OnlyThinLines", typeof(bool));
                }
                catch (SerializationException)
                {
                    OnlyThinLines = false;
                }
                modelView = null;
            }
            /// <summary>
            /// Implements <see cref="ISerializable.GetObjectData"/>
            /// </summary>
            /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
            /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                if (modelView != null)
                {
                    Name = modelView.Name;
                    VisibleLayers = new ArrayList(modelView.ProjectedModel.GetVisibleLayers());
                    Model = modelView.Model;
                    Projection = modelView.Projection;
                    //OnlyThinLines = modelView.ProjectedModel.gdiResources.OnlyThinLines;
                    // hier noch mehr Eigenschaften übernehmen, speicherbar machen
                    // und beim Erzeugen des ModelView verwenden.
                }
                info.AddValue("Name", Name, typeof(string));
                info.AddValue("Model", Model, typeof(Model));
                info.AddValue("Projection", Projection, typeof(Projection));
                if (VisibleLayers != null) info.AddValue("VisibleLayers", VisibleLayers, typeof(ArrayList));
                info.AddValue("OnlyThinLines", OnlyThinLines, typeof(bool));
            }
            #endregion
        }
        //private ArrayList modelViews; // Liste von ModelViewDescription
        private int activeModelIndex;
        private string activeViewName; // zum Abspeichern, welches war die aktive Ansicht

        private bool isModified;
        private Hashtable attributeLists;
        private FilterList filterList;
        private string fileName;
        private bool openedAsJson = false;
        private UndoRedoSystem undoRedoSystem;
        private GeoObjectList symbolList;
        private NamedValuesProperty namedValues;

        // Daten nur um zu deserialisieren von älteren Versionen
        private ArrayList ser_layoutlist;
        private ArrayList ser_modelViews;
        private ProjectedModel[] ser_projectedModels;
        private AnimatedView[] ser_animatedViews;
#if !WEBASSEMBLY
        private Layout[] ser_layouts;
        private GDI2DView[] ser_gdiViews;
#endif
        internal PrintDocument printDocument; // das PrintDocument zum Drucken, enthält die Druckereinstellungen
        /// <summary>
        /// Creates an empty Project. The empty project contains clones of the globally defined
        /// attributes and attribute lists (like colors, layers etc.)
        /// </summary>
        protected Project()
        {
            models = new ArrayList();
            attributeLists = new Hashtable();
            activeModelIndex = -1;

#if !WEBASSEMBLY
            layouts = new List<Layout>();
            gdiViews = new List<GDI2DView>();
#endif
            //modelViews = new ArrayList();
            projectedModels = new List<ProjectedModel>();
            animatedViews = new List<AnimatedView>();

            for (int i = 0; i < (Settings.GlobalSettings as IAttributeListContainer).ListCount; ++i)
            {
                IAttributeList atrlist = (Settings.GlobalSettings as IAttributeListContainer).List(i);
                (this as IAttributeListContainer).Add((Settings.GlobalSettings as IAttributeListContainer).ListKeyName(i), atrlist.Clone());
            }
            // wird von der Schleife vorher erledigt, auch fremde Attributlisten gehen diesen Weg
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"ColorList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"LayerList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"LineWidthList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"LinePatternList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"HatchStyleList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"DimensionStyleList",true);
            //AttributeListContainer.CloneAttributeList(Settings.GlobalSettings,this,"StyleList",true);
            AttributeListContainer.UpdateLists(this, true);

            filterList = new FilterList(); // auch aus globale settings?
            filterList.AttributeListContainer = this;
            base.resourceId = "ProjectSettings";

            UserData = new UserData();
            UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataRemoved);
            undoRedoSystem = new UndoRedoSystem();

            symbolList = new GeoObjectList();
            namedValues = new NamedValuesProperty();
        }
        ~Project()
        {
            // System.Diagnostics.Trace.WriteLine("destructing project: " + fileName);
        }
        /// <summary>
        /// Adds the given <see cref="Model"/> to the project. The attributes of the GeoObjects
        /// in this model are merged into the attribute lists of this project. Fires a
        /// <see cref="ModelsChangedEvent"/>.
        /// </summary>
        /// <param name="ToAdd">Model to add</param>
        public void AddModel(Model ToAdd)
        {
            models.Add(ToAdd);
            foreach (IGeoObject go in ToAdd) go.UpdateAttributes(this);
            AttributeListContainer.UpdateLists(this, true);
            ToAdd.Undo = undoRedoSystem;
            if (ModelsChangedEvent != null) ModelsChangedEvent(this, ToAdd, true);
            ToAdd.GeoObjectAddedEvent += new Model.GeoObjectAdded(OnGeoObjectAddedToModel);
            ToAdd.GeoObjectRemovedEvent += new Model.GeoObjectRemoved(OnGeoObjectRemovedFromModel);
            ToAdd.GeoObjectDidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            ToAdd.NameChangedEvent += new Model.NameChangedDelegate(OnModelNameChanged);

            // kann durch das Undo aufgerufen werden, deshalb hier ein Refresh an die etwas
            // versteckte ModelsProperty in den ShowProperties
            if (propertyTreeView != null)
            {
                if (ShowProperties != null)
                {
                    for (int i = 0; i < ShowProperties.Length; ++i)
                    {
                        ModelsProperty mp = ShowProperties[i] as ModelsProperty;
                        if (mp != null) mp.Refresh();
                    }
                }
                bool modelViewExists = false;
                foreach (ProjectedModel pm in projectedModels)
                {
                    if (pm.Model == ToAdd) modelViewExists = true;
                }
                //foreach (ModelViewDescription mvd in modelViews)
                //{
                //    if (mvd.Model == ToAdd) modelViewExists = true;
                //}

                // warum automatisch einen neuen ModelView generieren?
                //if (!modelViewExists)
                //{
                //    OnNewModelView(ToAdd);
                //    if (ShowProperties != null)
                //    {
                //        for (int i = 0; i < ShowProperties.Length; ++i)
                //        {
                //            ModelsProperty mp = ShowProperties[i] as ModelsProperty;
                //            if (mp != null)
                //            {
                //                propertyTreeView.OpenSubEntries(mp, true);
                //                mp.Refresh();
                //            }
                //        }
                //    }
                //}
            }
        }

        void OnModelNameChanged(Model sender, string newName)
        {
            if (ModelsChangedEvent != null) ModelsChangedEvent(this, sender, false);
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            this.isModified = true;
        }
        private void OnGeoObjectRemovedFromModel(IGeoObject go)
        {
            this.isModified = true;
        }
        private void OnGeoObjectAddedToModel(IGeoObject go)
        {
            this.isModified = true;
        }
        /// <summary>
        /// Removes the given <see cref="Model"/> from the project. Fires a
        /// <see cref="ModelsChangedEvent"/>.
        /// </summary>
        /// <param name="ToRemove">model to remove</param>
        public void RemoveModel(Model ToRemove)
        {
            undoRedoSystem.AddUndoStep(new ReversibleChange(this, "AddModel", ToRemove));
            models.Remove(ToRemove);
            ToRemove.Undo = null;
            if (ModelsChangedEvent != null) ModelsChangedEvent(this, ToRemove, false);
            ToRemove.GeoObjectAddedEvent -= new Model.GeoObjectAdded(OnGeoObjectAddedToModel);
            ToRemove.GeoObjectRemovedEvent -= new Model.GeoObjectRemoved(OnGeoObjectRemovedFromModel);
            ToRemove.GeoObjectDidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            ToRemove.NameChangedEvent -= new Model.NameChangedDelegate(OnModelNameChanged);
        }
        /// <summary>
        /// Removes the <see cref="Model"/> with the given index from the project. Fires a
        /// <see cref="ModelsChangedEvent"/>.
        /// </summary>
        /// <param name="Index">Index of the model to remove</param>
        public void RemoveModel(int Index)
        {
            Model m = models[Index] as Model;
            RemoveModel(m);
        }
        /// <summary>
        /// Returns the <see cref="Model"/> with the given index.
        /// </summary>
        /// <param name="Index">Index of the required model</param>
        /// <returns>the model</returns>
        public Model GetModel(int Index)
        {
            return (Model)(models[Index]);
        }
        /// <summary>
        /// Returns the number of models in this project.
        /// </summary>
        /// <returns>the number of models </returns>
        public int GetModelCount()
        {
            return models.Count;
        }
        /// <summary>
        /// Returns the active <see cref="Model"/> of this project.
        /// </summary>
        /// <returns>the active model</returns>
        public Model GetActiveModel()
        {
            if (activeModelIndex < 0) activeModelIndex = models.Count - 1;
            try
            {
                return (Model)models[activeModelIndex];
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
#if !WEBASSEMBLY
        /// <summary>
        /// Gets a default <see cref="Layout"/> for this project. If there is no layout
        /// in this project a standard Layout is created and adde to the project
        /// </summary>
        /// <returns>a default layout</returns>
        public Layout GetDefaultLayout()
        {
            if (layouts.Count == 0)
            {
                Layout l = new Layout(this);
                l.Name = StringTable.GetString("Layout.Default.Name");
                l.PaperWidth = 297;
                l.PaperHeight = 210;
                Border bdr = Border.MakeRectangle(10.0, 287, 10.0, 200); // 10mm Rand
                Projection pr = new Projection(Projection.StandardProjection.FromTop);
                Model m = models[0] as Model;
                BoundingRect ext = m.GetExtent(pr);
                double f = 1.0;
                while (f * ext.Width > l.PaperWidth || f * ext.Height > l.PaperHeight)
                {	// kleiner machen
                    f = f / 10.0;
                }
                ModOp scl = ModOp.Scale(f);
                ModOp trn = ModOp.Translate(l.PaperWidth / 2.0 - f * ext.Width / 2.0, l.PaperHeight / 2.0 - f * ext.Height / 2.0, 0.0);
                // pr.PrependModOp(trn*scl);
                pr.SetPlacement(f, l.PaperWidth / 2.0 - f * ext.Width / 2.0, l.PaperHeight / 2.0 - f * ext.Height / 2.0);
                l.AddPatch(m, pr, bdr);
                layouts.Add(l);
            }
            return layouts[0];
        }
        /// <summary>
        /// Gets the number of layouts in this project
        /// </summary>
        public int LayoutCount
        {
            get
            {
                return layouts.Count;
            }
        }
        /// <summary>
        /// Returns the <see cref="Layout"/> with the given index.
        /// </summary>
        /// <param name="index">index of required layout</param>
        /// <returns>layout</returns>
        public Layout GetLayout(int index)
        {
            return layouts[index];
        }
#endif
        /// <summary>
        /// Returns the number of <see cref="ModelViews"/> in this project
        /// </summary>
        internal int ModelViewCount
        {
            get
            {
                return projectedModels.Count;
            }
        }
        public List<AnimatedView> AnimatedViews
        {
            get
            {
                return animatedViews;
            }
        }
#if !WEBASSEMBLY
        /// <summary>
        /// Gets a list of all defined GDI2DViews in this project.
        /// </summary>
        public List<GDI2DView> GdiViews
        {
            get
            {
                if (gdiViews== null) { gdiViews= new List<GDI2DView>(); }
                return gdiViews;
            }
        }
#endif
        internal ProjectedModel GetProjectedModel(int index)
        {
            return projectedModels[index];
        }
        //internal ModelView GetModelView(int index, SimpleControl ctrl)
        //{
        //    ModelViewDescription mvd = (ModelViewDescription)modelViews[index];
        //    return mvd.GetModelView(ctrl);
        //}
        internal string GetModelViewName(int index)
        {
            return projectedModels[index].Name;
            //ModelViewDescription mvd = (ModelViewDescription)modelViews[index];
            //return mvd.Name;
        }
        internal bool RenameModelView(ModelView toRename, string newName)
        {
            for (int i = 0; i < projectedModels.Count; ++i)
            {
                if (toRename.ProjectedModel != projectedModels[i] && projectedModels[i].Name == newName) return false;
            }
            toRename.ProjectedModel.Name = newName;
            //ModelViewDescription mvdRename = null;
            //foreach (ModelViewDescription mvd in modelViews)
            //{
            //    ModelView mv = mvd.GetModelView(null);
            //    if (mv!=toRename && newName==mv.Name) return false;
            //    if (mv==toRename) mvdRename = mvd;
            //}
            //toRename.Name = newName;
            //mvdRename.Name = newName;
            return true;
        }
#if !WEBASSEMBLY
        /// <summary>
        /// Removes the given <see cref="Layout"/> from the project. There must always be at least
        /// one layout, so you cannot remove the last layout.
        /// </summary>
        /// <param name="toRemove">layout to remove</param>
        /// <returns>success: true, failure: false</returns>
        public bool RemoveLayout(Layout toRemove)
        {
            if (layouts.Count < 2) return false; // einer muss bestehen bleiben!
            bool found = false;
            for (int i = 0; i < layouts.Count; i++)
            {
                if (layouts[i] == toRemove)
                {
                    layouts.RemoveAt(i);
                    found = true;
                    // versuchsweise ausgeklammert um den Parameter "layoutView" zu eliminieren
                    //if (Frame != null)
                    //{
                    //    if (Frame.ActiveView == layoutView)
                    //    {
                    //        if ((Frame.ActiveView as LayoutView).Layout == toRemove)
                    //        {
                    //            Frame.ActiveView = null;
                    //        }
                    //    }
                    //}
                    if (ViewChangedEvent != null) ViewChangedEvent(this, null);
                    break;
                }
            }
            return found;
        }
#endif
        /// <summary>
        /// Removes the given ModelView from the project. There must always be at least
        /// one modelview, so you cannot remove the last modelview.
        /// </summary>
        /// <param name="toRemove">modelview to remove</param>
        /// <returns>success: true, failure: false</returns>
        public bool RemoveModelView(ModelView toRemove)
        {
            //if (modelViews.Count < 2) return false; // einer muss bestehen bleiben!
            if (projectedModels.Count < 2) return false; // einer muss bestehen bleiben!
            for (int i = 0; i < projectedModels.Count; ++i)
            {
                if (toRemove.ProjectedModel == projectedModels[i])
                {
                    projectedModels.RemoveAt(i);
                    if (Frame != null)
                    {
                        if (Frame.ActiveView == toRemove)
                        {
                            throw new ApplicationException("must be implemented");
                            // Frame.ActiveView = (modelViews[0] as ModelViewDescription).modelView; // den muss es geben
                        }
                    }
                    if (ViewChangedEvent != null) ViewChangedEvent(this, null);
                    break;
                }
            }
            //for (int i = 0; i < modelViews.Count; i++)
            //{
            //    ModelViewDescription mvd = modelViews[i] as ModelViewDescription;
            //    if (toRemove == mvd.modelView)
            //    {
            //        modelViews.RemoveAt(i);
            //        if (Frame!=null)
            //        {
            //            if (Frame.ActiveView==toRemove)
            //            {
            //                Frame.ActiveView = (modelViews[0] as ModelViewDescription).modelView; // den muss es geben
            //            }
            //        }                                        
            //        if (ViewChangedEvent != null) ViewChangedEvent(this, null);
            //        break;
            //    }
            //}
            return true;
        }
        /// <summary>
        /// Returns the <see cref="Model"/> with the given name, Returns null if there is no
        /// such model in the project.
        /// </summary>
        /// <param name="name">name of the required model</param>
        /// <returns>the model found or null</returns>
        public Model FindModel(string name)
        {
            // warum war das da? Gibt einen Absturz wenn man in der Ansicht ein Modell auswählt
            //for (int i = 0; i < projectedModels.Count; ++i)
            //{
            //    if (projectedModels[i].Name == name)
            //    {
            //        return projectedModels[i].Model;
            //    }
            //}
            foreach (Model m in models)
            {
                if (m.Name == name) return m;
            }
            return null;
        }
        //public ModelView FindModelView(string name)
        //{

        //    foreach (ModelViewDescription mvd in modelViews)
        //    {
        //        ModelView mv = mvd.GetModelView(null);
        //        if (name==mv.Name) return mv;
        //    }
        //    return null;
        //}
#if !WEBASSEMBLY
        internal bool FindLayoutName(string name)
        {
            foreach (Layout l in layouts)
            {
                if (l.Name == name) return true;
            }
            return false;
        }
#endif
        internal bool FindModelViewName(string name)
        {
            for (int i = 0; i < projectedModels.Count; ++i)
            {
                if (projectedModels[i].Name == name) return true;
            }
            //foreach (ModelViewDescription mvd in modelViews)
            //{
            //    if (name==mvd.Name) return true;
            //}
            return false;
        }
#if !WEBASSEMBLY
        /// <summary>
        /// Adds the specified <see cref="Layout"/> to the project.
        /// </summary>
        /// <param name="l">the layout to add</param>
        /// <returns>the index of the new layout</returns>
        public int AddLayout(Layout l)
        {
            layouts.Add(l);
            l.project = this;
            return layouts.Count - 1;
        }
        /// <summary>
        /// Renames the 
        /// </summary>
        /// <param name="l"></param>
        /// <param name="newName"></param>
        /// <returns></returns>
        public bool RenameLayout(Layout l, string newName)
        {
            foreach (Layout layout in layouts)
            {
                if (layout != l)
                {
                    if (layout.Name == newName) return false;
                }
            }
            l.Name = newName;
            return true;
        }
#endif
        public int AddProjectedModel(string name, Model model, Projection projection)
        {

            ProjectedModel pm = new ProjectedModel(model, projection);
            pm.Name = name;
            projectedModels.Add(pm);
            return projectedModels.Count - 1;

            //ModelViewDescription mvd = new ModelViewDescription(this);
            //mvd.Name = name;
            //mvd.Model = model;
            //mvd.Projection = projection;
            //mvd.modelView = null;
            //modelViews.Add(mvd);
            //return modelViews.Count-1;
        }
        internal ProjectedModel AddProjectedModel(Model model)
        {
            OnNewModelView(model);
            return projectedModels[projectedModels.Count - 1];
        }
        internal void SetModelView(ProjectedModel pm)
        {
            for (int i = 0; i < projectedModels.Count; i++)
            {
                if (projectedModels[i].Name == pm.Name)
                {
                    projectedModels[i] = pm;
                    break;
                }
            }
        }
        public void RemoveModelView(int index)
        {
            projectedModels.RemoveAt(index);
            //modelViews.RemoveAt(index);
        }
        // hier fehlen noch die Add Methoden um neue Layouts und Modelviews zuzufügen
        internal void AssureViews()
        {	// stellt sicher, dass es wenigstens einen ModelView und einen LayoutView gibt
#if !WEBASSEMBLY
            if (layouts.Count == 0)
            {
                Layout l = new Layout(this);
                l.Name = StringTable.GetFormattedString("Layout.Default.Name", 1);
                l.PaperWidth = 297;
                l.PaperHeight = 210;
                // Border bdr = Border.MakeRectangle(10.0,287,10.0,200); // 10mm Rand
                Projection pr = new Projection(Projection.StandardProjection.FromTop);
                Model m = models[activeModelIndex] as Model;
                BoundingRect ext = m.Extent.GetExtent(pr);
                // wir können hier nicht auf den echten 2d extent zurückgreifen, da der 
                // die triangulierung benutzt.
                // BoundingRect ext = m.GetExtent(pr);
                double f = 1.0;
                while (f * ext.Width > l.PaperWidth || f * ext.Height > l.PaperHeight)
                {	// kleiner machen
                    f = f / 10.0;
                }
                ModOp scl = ModOp.Scale(f);
                double dx, dy;
                if (ext.IsEmpty())
                {
                    dx = 0.0;
                    dy = 0.0;
                }
                else
                {
                    dx = l.PaperWidth / 2.0 - f * ext.Width / 2.0 - ext.Left * f;
                    dy = l.PaperHeight / 2.0 - f * ext.Height / 2.0 - ext.Bottom * f;
                }
                pr.SetPlacement(f, dx, dy);
                l.AddPatch(m, pr, null);
                layouts.Add(l);
            }
#endif
            if (projectedModels.Count == 0)
            {
                ProjectedModel pm = new ProjectedModel(models[activeModelIndex] as Model, new Projection(Projection.StandardProjection.FromTop));
                pm.Name = StringTable.GetFormattedString("ModelView.Default.Name", 1);
                projectedModels.Add(pm);
            }
            //if (modelViews.Count==0)
            //{
            //    ModelViewDescription mvd = new ModelViewDescription(this);
            //    mvd.Name = StringTable.GetFormattedString("ModelView.Default.Name",1);
            //    mvd.Model = models[activeModelIndex] as Model;
            //    mvd.Projection = new Projection(Projection.StandardProjection.FromTop);
            //    mvd.Projection.DrawingPlane = Plane.XYPlane;
            //    mvd.VisibleLayers = new ArrayList(LayerList); // alle sichtbar
            //    modelViews.Add(mvd);
            //}
        }
        internal void ResetViews()
        {
            //modelViews.Clear();
            projectedModels.Clear();
#if !WEBASSEMBLY
            layouts.Clear();
#endif
            AssureViews();
        }
        internal string ActiveViewName
        {
            get
            {
                return activeViewName;
            }
            set
            {
                activeViewName = value;
            }
        }
        public GeoObjectList AdditionalSymbols { get { return symbolList; } }
        /// <summary>
        /// The <see cref="CADability.UserData"/> object can take any kind of data. If the data objects
        /// are serializable i.e. implement <see cref="System.Runtime.Serialization.ISerializable"/> they are serialized
        /// together with the project. If they implement the <see cref="IPropertyEntry"/> interface
        /// they are displayed and can be modified on the project tab page of the control center.
        /// </summary>
        public UserData UserData;
        public FilterList FilterList
        {
            get
            {
                return filterList;
            }
        }
        public GeoObjectList SymbolList
        {
            get
            {
                return symbolList;
            }
        }
        public object GetNamedValue(string name)
        {
            return namedValues.GetNamedValue(name);
        }
        public void SetNamedValue(string name, object val)
        {
            namedValues.SetNamedValue(name, val);
        }
        internal NamedValuesProperty NamedValues
        {
            get
            {
                return namedValues;
            }
        }
        /// <summary>
        /// Gets or sets the modified flag
        /// </summary>
        public bool IsModified
        {
            get { return isModified; }
            set { isModified = value; }
        }
        /// <summary>
        /// The name of the file in which this project ist stored. May be null.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }
        public void SetActiveModel(int Index)
        {
            if (Index < 0 || Index >= models.Count) throw new ProjectException(ProjectException.tExceptionType.InvalidModelIndex);
            activeModelIndex = Index;
        }
        /// <summary>
        /// Liefert die Standard Darstellung des gegebenen Modells.
        /// Wenn keine definiert ist, die Draufsicht.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public ModelView GetStandardModelView(Model m)
        {
            // noch nicht richtig implementiert
            ModelView view = new ModelView(this);
            view.ProjectedModel = new ProjectedModel(m, new Projection(Projection.StandardProjection.FromTop));
            view.Projection.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            return view;
        }
        public ModelView GetStandardModelView()
        {
            // noch nicht richtig implementiert
            ModelView view = new ModelView(this);
            view.ProjectedModel = new ProjectedModel(GetActiveModel(), new Projection(Projection.StandardProjection.FromTop));
            view.Projection.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            return view;
        }
        public ModelView GetStandardModelView(Model m, int Index)
        {
            Projection pr;
            switch (Index)
            {
                default:
                case 0:
                    pr = new Projection(Projection.StandardProjection.FromTop);
                    pr.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
                    break;
                case 1:
                    pr = new Projection(Projection.StandardProjection.FromRight);
                    pr.DrawingPlane = new Plane(Plane.StandardPlane.YZPlane, 0.0);
                    break;
                case 2:
                    pr = new Projection(Projection.StandardProjection.FromFront);
                    pr.DrawingPlane = new Plane(Plane.StandardPlane.XZPlane, 0.0);
                    break;
                case 3:
                    pr = new Projection(new GeoVector(-1.0, -1.0, -1.0), new GeoVector(0.0, 0.0, 1.0));
                    GeoVector dirX = new GeoVector(0.0, 0.0, 1.0) ^ new GeoVector(-1.0, -1.0, -1.0);
                    GeoVector dirY = dirX ^ new GeoVector(-1.0, -1.0, -1.0);
                    pr.DrawingPlane = new Plane(new GeoPoint(0.0, 0.0, 0.0), dirX, dirY);
                    break;
            }
            ModelView view = new ModelView(this);
            view.ProjectedModel = new ProjectedModel(m, pr);
            return view;
        }
        /// <summary>
        /// Überprüft, ob Daten verändert wurden (IsModified) und fordert ggf. den Anwender auf
        /// das Projekt zu speichern. 
        /// </summary>
        /// <returns>false, wenn Abbrechen gedrückt wurde, true sonst (speichern oder verwerfen)</returns>
        public virtual bool SaveModified()
        {
            if (IsModified)
            {

                string msg = StringTable.GetString("Project.SaveModified");
                if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
                {
                    switch (Frame.UIService.ShowMessageBox(msg, "CADability", MessageBoxButtons.YesNoCancel))
                    {
                        case DialogResult.Yes:
                            return WriteToFile(FileName);
                        case DialogResult.No:
                            IsModified = false; // damit bei "nein" nicht zweimal aufgerufen wird
                            return true;
                        case DialogResult.Cancel:
                            return false;
                    }
                }
                else
                {
                    return WriteToFile(FileName);
                }
            }
            return true;
        }
        static public Project CreateSimpleProject()
        {
            Project res = Construct();
            Model m = new Model();
            object o = Settings.GlobalSettings.GetValue("DefaultModelSize");
            if (o != null)
            {
                if (o is BoundingCube) m.Extent = (BoundingCube)o;
                else if (o is List<object> lo) m.Extent = new BoundingCube((double)lo[0], (double)lo[1], (double)lo[2], (double)lo[3], (double)lo[4], (double)lo[5]);
                else m.Extent = new BoundingCube(0, 100, 0, 100, 0, 100);
            }
            else
            {
                m.Extent = new BoundingCube(0, 100, 0, 100, 0, 100);
            }
            res.AddModel(m);
            res.SetActiveModel(0);
            // Settings.GlobalSettings.GetBoolValue("... wenn man folgendes nicht will, dann über globalsettings abschalten
            string[] names = StringTable.GetSplittedStrings("Projection.Default");
            for (int i = 0; i < names.Length; ++i)
            {
                Projection pr;
                switch (i)
                {
                    default:
                    case 0:
                        pr = new Projection(Projection.StandardProjection.FromTop);
                        break;
                    case 1:
                        pr = new Projection(Projection.StandardProjection.FromRight);
                        break;
                    case 2:
                        pr = new Projection(Projection.StandardProjection.FromFront);
                        break;
                    case 3:
                        pr = new Projection(new GeoVector(-1, -1, -1), GeoVector.ZAxis);
                        pr.DrawingPlane = Plane.XYPlane;
                        break;
                }
                res.AddProjectedModel(names[i], m, pr);
            }
            return res;
        }
        public UndoRedoSystem Undo
        {
            get { return undoRedoSystem; }
        }
        /// <summary>
        /// Write the project to an XML file using a soap formatter with System.Runtime.Serialization. Use <see cref="ReadFromXML"/> to read the file into a projact.
        /// </summary>
        /// <param name="FileName">Name of the file to write to.</param>
        public void WriteToXML(string FileName)
        {
            throw new NotImplementedException("Writing to XML no longer supported, write to JSON instead");
            //// BinaryFormatter formatter = new BinaryFormatter();
            //Stream stream = File.Open(FileName, FileMode.Create);
            //SoapFormatter soapformatter = new SoapFormatter(); // funktioniert, aber sehr geschwätzig
            //soapformatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            //soapformatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesWhenNeeded;
            //soapformatter.Serialize(stream, this);
            //stream.Close();
        }
        /// <summary>
        /// Read the provided file with a sop formatter into a new Project. The file must have been written with <see cref="WriteToXML"/>.
        /// </summary>
        /// <param name="FileName"></param>
        /// <returns></returns>
        public static Project ReadFromXML(string FileName)
        {
            throw new NotImplementedException("Reading from XML no longer supported, use JSON instead");
            //// BinaryFormatter formatter = new BinaryFormatter();
            //Stream stream = File.Open(FileName, FileMode.Open);
            //SoapFormatter soapformatter = new SoapFormatter(); // funktioniert, aber sehr geschwätzig
            //soapformatter.Binder = new CondorSerializationBinder(); // nur zum Test
            //soapformatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            //soapformatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesWhenNeeded;
            //Project res = soapformatter.Deserialize(stream) as Project;
            //stream.Close();
            //return res;
        }
        /// <summary>
        /// Writes the project data to the given stream. Resets the IsModified flag
        /// </summary>
        /// <param name="stream">Stream where to write the data</param>
        public void WriteToStream(Stream stream)
        {
            // BinaryFormatter formatter = new BinaryFormatter();
            // System.Xml.Serialization.XmlSerializer xml = new System.Xml.Serialization.XmlSerializer(typeof(Project));
            // xml.Serialize(stream, this); // geht nicht, macht vermutlich keinen Object-Graph

            // SOAPFORMATTER: liefert sehr geschwätzigen output. Am einfachsten dürfte es sein, diesen output
            // in ein XML Dokument umzuwandeln und darauf einige Operationen auszuführen, die die NameSpaces
            // entfernen. Die dazu inversen Operationen müssen natürlich auch vorhanden sein, um das Zeug wieder
            // einlesen zu können.
            //SoapFormatter soapformatter = new SoapFormatter(); // funktioniert, aber sehr geschwätzig
            //soapformatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            //soapformatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesWhenNeeded;
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File, null));
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            // formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString; // XsdString macht das wechseln von .NET Frameworks schwierig
            formatter.Serialize(stream, this);
            // soapformatter.Serialize(stream, this);
            isModified = false;
        }
        /// <summary>
        /// Saves the project in a file with the given FileName.
        /// If FileName is null, a SaveFileDialog is presented. Uses WriteToStream.
        /// </summary>
        /// <param name="FileName">The file name</param>
        /// <returns>true, if successful, false if the user pressed escape in the SaveFileDialog</returns>
        virtual public bool WriteToFile(string FileName)
        {
            // Für die ThumbNail Ansicht braucht man IExtractImage
            bool writeAsJson = openedAsJson;
            if (Settings.GlobalSettings.GetBoolValue("SaveMode.SaveAsJson", false)) writeAsJson = true;
            writeAsJson = true;
            if (FileName == null || FileName.Length == 0)
            {
                int filterIndex = 0;
                if (Frame.UIService.ShowSaveFileDlg("Project.WriteToFile", StringTable.GetString("MenuId.File.Save.As"), StringTable.GetString("File.CADability.Filter.json"), ref filterIndex, ref FileName) == Substitutes.DialogResult.OK)
                {
                    writeAsJson = true; // the json Format is the only format we accept , no more writing in binary ISerializable format
                }
                else return false;
            }
            if (FileName != null && FileName.Length > 0)
            {
                fileName = FileName;
                Stream stream = File.Open(FileName, FileMode.Create);
                if (writeAsJson)
                {
                    JsonSerialize js = new JsonSerialize();
                    js.ToStream(stream, this);
                }
                else
                {
                    WriteToStream(stream);
                }
                stream.Close();
            }
            return true;
        }

        public bool WriteToFileWithoutUserData(string fileName)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                WriteToStream(ms);
                ms.Seek(0, SeekOrigin.Begin);
                Project pr = Project.ReadFromStream(ms);
                pr.UserData.Clear();
                foreach (Model m in pr)
                {
                    m.UserData.Clear();
                    foreach (IGeoObject go in m)
                    {
                        RemoveUserData(go);
                    }
                }
                return pr.WriteToFile(fileName);
            }
            catch
            {
                return false;
            }
        }

        private void RemoveUserData(IGeoObject go)
        {
            go.UserData.Clear();
            for (int i = 0; i < go.NumChildren; i++)
            {
                RemoveUserData(go.Child(i));
            }
        }
        /// <summary>
        /// Export the project in one of the following formats:
        /// dxf, dwg, iges, step, vrml, stl, sat and xt (sat and xt must be licensed seperately) 
        /// </summary>
        /// <param name="fileName">Path and filename for the generated output file</param>
        /// <param name="format">Format, one of the strings: dxf, dwg, iges, step, vrml, stl, sat, xt </param>
        /// <returns>true on success</returns>
        public bool Export(string fileName, string format)
        {
            format = format.ToLower();
            switch (format)
            {
                case "cdb": return WriteToFile(fileName);
                case "html":
                    return true;
                case "dxf":
                    CADability.DXF.Export export = new DXF.Export(netDxf.Header.DxfVersion.AutoCad2000);
                    export.WriteToFile(this, fileName);
                    return true;
                case "dwg":
                    return true;
                case "dxb":
                    return true;
                case "igs":
                case "iges":
                    break;
                case "step":
                case "stp":
                    ExportStep exportStep = new ExportStep();
                    exportStep.WriteToFile(fileName, this);
                    break;
                case "vrml":
                case "wrl":
                    break;
                case "stl":
                    ExportToSTL(fileName);
                    break;
                case "brep":
                    break;
                case "sat":
                    break;
                case "xt":
                case "x_t":
                    break;
            }
            return false;
        }
        public void ExportToSTL(string fileName)
        {
            Model m = GetActiveModel();

            using (PaintToSTL pstl = new PaintToSTL(fileName, Settings.GlobalSettings.GetDoubleValue("Export.STL.Precision", 0.005)))
            {
                pstl.Init();
                for (int i = 0; i < m.Count; i++)
                {
                    m[i].PaintTo3D(pstl);
                }
            }
        }
#if !WEBASSEMBLY
        public void ExportToWebGl(string fileName)
        {
            ExportToWebGl wgl = new ExportToWebGl(GetActiveModel());
            if (FrameImpl.MainFrame != null && FrameImpl.MainFrame.ActiveView != null)
            {
                GeoVector pdir = FrameImpl.MainFrame.ActiveView.Projection.Direction;
                wgl.InitialProjectionDirection = pdir;
            }
            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            int lastSlash = ThisAssembly.Location.LastIndexOf('\\');
            string path = ThisAssembly.Location.Substring(0, lastSlash) + "\\WebGLS.html";
            wgl.HtmlTemplatePath = path;
            wgl.WriteToFile(fileName);
        }
#endif

        private class CondorSerializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                System.Diagnostics.Trace.WriteLine("BindToType: " + assemblyName + ", " + typeName);
                // Diese Zeilen dienen dazu alte CONDOR Dateien lesbar zu machen.
                // evtl. machen sie Schwierigkeiten, wenn Objekte von anderen Modulen
                // deserialisiert werden sollen. Dann müsste man weiter unten es wieder 
                // rausnehmen (CondorSerializationBinder)
                if (typeName.StartsWith("Condor."))
                {
                    typeName = typeName.Replace("Condor.", "CADability.");
                }
                if (assemblyName.StartsWith("Condor"))
                {
                    assemblyName = assemblyName.Replace("Condor", "CADability");
                }
                if (typeName.StartsWith("CADability.DimensionStyle"))
                {
                    typeName = typeName.Replace("CADability.DimensionStyle", "CADability.Attribute.DimensionStyle");
                }
                if (typeName.StartsWith("CADability.HatchStyle"))
                {
                    typeName = typeName.Replace("CADability.HatchStyle", "CADability.Attribute.HatchStyle");
                }
                if (typeName.StartsWith("CADability.LineWidth"))
                {
                    typeName = typeName.Replace("CADability.LineWidth", "CADability.Attribute.LineWidth");
                }
                if (typeName.StartsWith("CADability.LinePattern"))
                {
                    typeName = typeName.Replace("CADability.LinePattern", "CADability.Attribute.LinePattern");
                }
                if (typeName.StartsWith("CADability.Filter"))
                {
                    typeName = typeName.Replace("CADability.Filter", "CADability.Attribute.Filter");
                }

                switch (typeName)
                {
                    case "CADability.Layout+Patch": // Klassennamen geändert von privater Klasse nach außerhalb
                        typeName = "CADability.LayoutPatch";
                        break;
                    case "CADability.GeoObjectList":
                        typeName = "CADability.GeoObject.GeoObjectList";
                        break;
                    case "CADability.ColorList":
                        typeName = "CADability.Attribute.ColorList";
                        break;
                    case "CADability.LayerList":
                        typeName = "CADability.Attribute.LayerList";
                        break;
                    case "CADability.LineWidthList":
                        typeName = "CADability.Attribute.LineWidthList";
                        break;
                    case "CADability.LinePatternList":
                        typeName = "CADability.Attribute.LinePatternList";
                        break;
                    case "CADability.HatchStyleList":
                        typeName = "CADability.Attribute.HatchStyleList";
                        break;
                    case "CADability.DimensionStyleList":
                        typeName = "CADability.Attribute.DimensionStyleList";
                        break;
                    case "CADability.StyleList":
                        typeName = "CADability.Attribute.StyleList";
                        break;
                    case "CADability.ColorDef":
                        typeName = "CADability.Attribute.ColorDef";
                        break;
                    case "CADability.Layer":
                        typeName = "CADability.Attribute.Layer";
                        break;
                    case "CADability.LineWidth":
                        typeName = "CADability.Attribute.LineWidth";
                        break;
                    case "CADability.LinePattern":
                        typeName = "CADability.Attribute.LinePattern";
                        break;
                    case "CADability.HatchStyle":
                        typeName = "CADability.Attribute.HatchStyle";
                        break;
                    case "CADability.DimensionStyle":
                        typeName = "CADability.Attribute.DimensionStyle";
                        break;
                    case "CADability.Style":
                        typeName = "CADability.Attribute.Style";
                        break;
                    case "CADability.ColorDef+ColorSource":
                        typeName = "CADability.Attribute.ColorDef+ColorSource";
                        break;
                    case "CADability.LinePattern+Scaling":
                        typeName = "CADability.Attribute.LinePattern+Scaling";
                        break;
                    case "CADability.ICurve":
                        typeName = "CADability.GeoObject.ICurve";
                        break;
                    case "CADability.Attribute.HatchStyleContour+HoleMode":
                        typeName = "CADability.Attribute.HatchStyleContour+EHoleMode";
                        break;
                }
                Type res = null;
                res = Type.GetType(typeName);
                if (res == null)
                {
                    if (Project.BindToTypeEvent != null) res = Project.BindToTypeEvent(assemblyName, typeName);
                }
                if (res == null && (typeName == "ErsaCAD.EDC_ProzessPunkt" || typeName == "ErsaCAD.EDC_PositionierBahn"))
                {
                    res = typeof(UnDeseriazableBlock);
                }
                if (res == null && !typeName.StartsWith("System."))
                {
                    res = typeof(UnDeseriazableObject);
                }
                if (typeName.StartsWith("System.Globalization.CompareInfo"))
                {
                    // res = typeof(SCompareInfo);
                }
                //if (typeName.StartsWith("System.Collections.ListDictionaryInternal"))
                //{
                //}

                return res;
            }
        }
        /// <summary>
        /// Delegate to enable the deserialization of objects that have been renamed or changed the version number.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly</param>
        /// <param name="typeName">neme of the type</param>
        /// <returns>The type of an object that can be constructed for deserialization</returns>
        public delegate Type BindToTypeDelegate(string assemblyName, string typeName);
        /// <summary>
        /// Event that gets called when a type cannot be resolved during the deserialization. Provide the Type
        /// of an constructable object as a result.
        /// </summary>
        static public event BindToTypeDelegate BindToTypeEvent;
        private static string cdbFixFw2;
        /* Serialisierung als JSON Objekt:
         * Man kann das ISerializable Interface und den Konstruktor mit SerializationInfo info, StreamingContext context dazu verwenden, alle bestehenden
         * Daten aus den Objekten zu speichern bzw. restaurieren. Siehe LaserParameter.cs: ReadISerializabelFromXml und SaveISerializabelToXml.
         * Dabei ist noch kein ObjectGraph verwirklicht. 
         * Implementierung des ObjectGraph: Alle Objekte kommen in ein Dictionary<object,int> und bekommen zwei zusätzliche Eigenschaften:
         * JSON_Type: der Typname und JSON_Id: der Index
         * Wenn ein Objekt serialisiert wird, dann wird unterschieden, ob wir uns im Root befinden, oder nicht:
         * Im Root werden die beiden Eigenschaften JSON_Type und JSON_Id und alle weiteren Eigenschaften geschrieben,
         * nicht im root wird nur die JSON_Id geschrieben. 
         * Das Lesen ist schwierig, wenn es einen Zyklus gibt: Im Konstruktor wird schon auf den Type gecastet, d.h. das Objekt muss schon da sein.
         * Aber man kann ja kein leeres Objekt erzeugen
         */
        /// <summary>
        /// Creates a new project reading the data from the given stream.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns></returns>
        public static Project ReadFromStream(Stream stream)
        {
            // BinaryFormatter formatter = new BinaryFormatter();
            FinishDeserialization finishDeserialization = new FinishDeserialization();
            BinaryFormatter formatter = new BinaryFormatter(); //(null,new StreamingContext(StreamingContextStates.File,finishDeserialization));
            // so kann man ggf. die ganze Serialisierung auf JSON umstellen:
            // SerializationInfo si = new SerializationInfo(typeof(Project), new FormatterConverter());
            // damit kann man GetData aufrufen und dann si wiederum nach zugefügten Einträgen iterieren
            // mit NewtonSoft (https://github.com/JamesNK/Newtonsoft.Json) scheint das mehr Sinn zu machen als mit DataContract

#if FW2
            formatter.Binder = new CondorSerializationBinder(); // das soll hier gleich laufen, damit nicht versucht wird die CADability.dll zu laden
#endif
            long startPosition = stream.Position;
            try
            {
                int tcstart = Environment.TickCount;
                // object o = formatter.Deserialize(stream);
                Project res = (Project)formatter.Deserialize(stream);
                int time = Environment.TickCount - tcstart;
                //System.Diagnostics.Trace.WriteLine("ReadTime: " + time.ToString());
                return res;
            }
            catch (TargetInvocationException e)
            {	// wenn irgend ein Konstruktor fehlschlägt, dann zeigt der Stacktrace
                // von TargetInvocationException.InnerException die eigentliche Ursache!
                // System.Diagnostics.Debug.WriteLine(e.InnerException.StackTrace);
                if (e.InnerException is ProjectSerializationException)
                {
                    if ((e.InnerException as ProjectSerializationException).ResourceId != null)
                    {
                        string msg = StringTable.GetString((e.InnerException as ProjectSerializationException).ResourceId);
                        //if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
                        //    if (msg != null) MessageBox.Show(msg);
                    }
                }
                return null;
            }
            catch (ProjectSerializationException e)
            {
                if (e.ResourceId != null)
                {
                    string msg = StringTable.GetString(e.ResourceId);
                    //if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
                    //    if (msg != null) MessageBox.Show(msg);
                }
                return null;
            }
            catch (Exception)
            {
                formatter = new BinaryFormatter(); // (null, new StreamingContext(StreamingContextStates.File, finishDeserialization));
                formatter.Binder = new CondorSerializationBinder();
                //formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
                //formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString;

                // Achtung: die folgenden Zeilen bringen das Deserialisieren auch durcheinander (sie bewirken, die CompareInfo exception):
                //SurrogateSelector ss = new SurrogateSelector();
                //ss.AddSurrogate(typeof(CompareInfo), new StreamingContext(StreamingContextStates.All), new CompareInfoSerializationSurrogate());
                //formatter.SurrogateSelector = ss;

                // Diesen Artikel lesen: http://msdn.microsoft.com/en-us/magazine/cc188950.aspx
                //formatter.FilterLevel = System.Runtime.Serialization.Formatters.TypeFilterLevel.Low;
                try
                {
                    stream.Position = startPosition;
                    Project res = (Project)formatter.Deserialize(stream);
                    // finishDeserialization.DeserializationDone();
                    return res;
                }
                catch (Exception ex)
                {
                    if (ex is ThreadAbortException) throw (ex);
                    if (ex is OutOfMemoryException) throw (ex);
#if FW2
#else
                    if (ex.Message.Contains("CompareInfo")) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is ArgumentNullException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is ArgumentOutOfRangeException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is OverflowException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is SerializationException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is OutOfMemoryException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is System.IO.FileLoadException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);

#endif

                    // System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    return null;
                }
            }
            // wenn hier was schiefgeht ist es schwer zu debuggen. In alle
            // Konstruktoren mit (SerializationInfo info, StreamingContext context)
            // müsste ein Writeln, damit man weiß, wo es aussteigt.
            // ACHTUNG: Verändern eines Namespaces in dem sich die zu serialisierenden 
            // Objekte befinden macht das Einlesen unmöglich. Dazu müsste man "SerializationBinder"
            // überschreiben und eine entsprechende tabelle benutzen.
        }
        private static Project ReadFromConvertedStream(FileStream stream)
        {   // hier unbedingt CondorSerializationBinder verwenden, denn sonst wird die "PureCADability.dll" in den Typnamen verwendet
            BinaryFormatter formatter = new BinaryFormatter(); //(null,new StreamingContext(StreamingContextStates.File,finishDeserialization));
            long startPosition = stream.Position;
            formatter.Binder = new CondorSerializationBinder();
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            //formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString;

            try
            {
                stream.Position = startPosition;
                Project res = (Project)formatter.Deserialize(stream);
                return res;
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException) throw (ex);
                return null;
            }
        }
        /// <summary>
        /// Creates a new project reading the data from the file with the given name.
        /// If FileName is null, an OpenFileDialog will be displayed.
        /// </summary>
        /// <param name="FileName">The name of the file</param>
        /// <returns>The newly created project or null if failed to create a project</returns>
        public static Project ReadFromFile(string FileName)
        {
            return ReadFromFile(FileName, true);
        }
        internal static Project ReadFromFile(string FileName, bool useProgress)
        {
            useProgress = useProgress && !Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false);
            if (FileName == null)
            {
                return null;
                //OpenFileDialog dlg = new OpenFileDialog();
                //dlg.RestoreDirectory = true;
                //dlg.Filter = StringTable.GetString("Project.OpenFileFilter");
                //if (dlg.ShowDialog()!=DialogResult.OK) return null;
                //FileName = dlg.FileName;
            }
            Project res = null;
            using (FileStream stream = File.Open(FileName, FileMode.Open, System.IO.FileAccess.Read))
            {
                int firstByte = stream.ReadByte();
                stream.Seek(0, SeekOrigin.Begin);
                if (firstByte == 123)
                {
                    res = ReadFromJson(stream);
                    if (res != null)
                    {
                        res.fileName = FileName;
                        res.openedAsJson = true;
                        stream.Close();
                        return res;
                    }
                }
                //Form.ActiveForm.Update();
                //ReadProgress progress = new ReadProgress(stream);
                //ProgressFeedBack pf = null;
                //if (useProgress)
                //{
                //    pf = new ProgressFeedBack();
                //    pf.Title = StringTable.GetFormattedString("ReadFile.Progress", FileName);
                //    pf.StreamPosition(50, stream);
                //    pf.Float(100);
                //}
                //ReadProgress.ShowProgressDelegate showProgressDelegate = new ReadProgress.ShowProgressDelegate(progress.ShowProgress);
                // ReadProgress funktioniert noch nicht perfekt. 
                try
                {
                    //if (pf != null) pf.Start();
                    //showProgressDelegate.BeginInvoke(null,null);
                    res = ReadFromStream(stream);
                    if (res == null)
                    {
                        stream.Close();

                        return null;
                    }
                    res.fileName = FileName;
                }
                catch (ProjectOldVersionException)
                {
                    stream.Close();
                    stream.Dispose();
                    if (cdbFixFw2 == null)
                    {
                        Assembly ThisAssembly = Assembly.GetExecutingAssembly();
                        int lastSlash = ThisAssembly.Location.LastIndexOf('\\');
                        if (lastSlash >= 0)
                        {
                            string path = ThisAssembly.Location.Substring(0, lastSlash);
                            lastSlash = path.LastIndexOf('\\');
                            //if (lastSlash >= 0) // das geht noch ein level zurück, wir erwarten die Anwendung im selben Verzeichnis wie CADability 
                            //{
                            //    path = path.Substring(0, lastSlash);
                            //}
                            string[] files = Directory.GetFiles(path, "cdbFixFw2.exe", SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                cdbFixFw2 = files[0];
                            }
                        }
                    }
                    if (cdbFixFw2 != null)
                    {
                        Process process = Process.Start(cdbFixFw2, "\"" + FileName + "\"");
                        if (process != null)
                        {
                            process.WaitForExit();
                            if (process.ExitCode == 0) // d.h. OK
                            {
                                return ReadConvertedFile(FileName, useProgress);
                            }
                        }
                    }
                }
                finally
                {
                    //if (pf != null) pf.Stop();
                    //progress.Finished();
                    stream.Close();
                }
                return res;
            }
        }
        private static Project ReadFromJson(FileStream stream)
        {
            JsonSerialize js = new JsonSerialize();
            Project res = js.FromStream(stream) as Project;
            AttributeListContainer.UpdateLists(res, true);
            return res;
        }
        private static Project ReadConvertedFile(string FileName, bool useProgress)
        {
            Project res = null;
            FileStream stream = File.Open(FileName, FileMode.Open);
            useProgress = useProgress && !Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false);
            //ProgressFeedBack pf = null;
            //if (useProgress)
            //{
            //    pf = new ProgressFeedBack();
            //    pf.Title = StringTable.GetFormattedString("ReadFile.Progress", FileName);
            //    pf.StreamPosition(50, stream);
            //    pf.Float(100);
            //}
            try
            {
                //if (pf != null) pf.Start();
                res = ReadFromConvertedStream(stream);
                if (res != null)
                {
                    res.fileName = FileName;
                }
            }
            finally
            {
                //if (pf != null) pf.Stop();
                stream.Close();
            }
            if (res != null) res.WriteToFile(FileName); // und gleich wieder rausschreiben, denn an den konvertierten Objekten hängt die falsche DLL
            return res;
        }
        /// <summary>
        /// If necessary, a dxf or dwg file is converted to ACAD2000 dxf version and the temporary filename is returned. Upon Dispose, the temporary files are deleted.
        /// There is a free converter program from open design https://www.opendesign.com/guestfiles/oda_file_converter (odafileconverter.exe).
        /// if this program is installed on the computer and the global setting "DwgDxfConverter" contains the path and filename to this program, it will be automatically executed
        /// and the dwg file will be converted to a dxf file, which can then be imported.
        /// </summary>
        private class ConvertToDxfAutoCad2000 : IDisposable
        {
            private string folderOrg, folderConverted;
            public string DxfFileName;
            public ConvertToDxfAutoCad2000(string fileName, string format)
            {
                if ((format.Equals("dxf", StringComparison.OrdinalIgnoreCase) && !DXF.Import.CanImportVersion(fileName)) || format.Equals("dwg", StringComparison.OrdinalIgnoreCase))
                {
                    string converter = Settings.GlobalSettings.GetStringValue("DwgDxfConverter", null);
                    bool isInSetting = true;
                    if (String.IsNullOrEmpty(converter))
                    {
                        isInSetting = false;

                        string programFilesPath = string.Empty;

                        if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
                        {
                            programFilesPath = Environment.GetEnvironmentVariable("ProgramW6432");
                        }
                        else
                        {
                            programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                        }

                        //converter = @"C:\Program Files\ODA\ODAFileConverter_title 21.3.0\ODAFileConverter.exe";                        
                        string odaFolder = System.IO.Path.Combine(programFilesPath, "ODA");
                        if (Directory.Exists(odaFolder))
                        {
                            string[] oda = Directory.GetFiles(odaFolder, "odafileconverter.exe", SearchOption.AllDirectories);

                            for (int i = 0; i < oda.Length; i++)
                            {
                                string fn = System.IO.Path.GetDirectoryName(oda[i]);
                                if (!string.IsNullOrEmpty(converter))
                                {
                                    string fnc = System.IO.Path.GetDirectoryName(converter);
                                    if (string.Compare(fnc, fn, true) < 0) converter = oda[i];
                                }
                                else
                                {
                                    converter = oda[i];
                                }
                            }
                        }
                    }
                    if (!String.IsNullOrEmpty(converter))
                    {
                        folderOrg = GetTemporaryDirectory();
                        folderConverted = GetTemporaryDirectory();
                        Directory.CreateDirectory(folderOrg);
                        Directory.CreateDirectory(folderConverted);
                        File.Copy(fileName, System.IO.Path.Combine(folderOrg, System.IO.Path.GetFileName(fileName)));
                        DxfFileName = System.IO.Path.Combine(folderConverted, System.IO.Path.GetFileNameWithoutExtension(fileName) + ".dxf");
                        System.Diagnostics.Process process = new System.Diagnostics.Process();
                        // Configure the process using the StartInfo properties.
                        process.StartInfo.FileName = converter;
                        process.StartInfo.Arguments = "\"" + folderOrg + "\" " + "\"" + folderConverted + "\" " + "ACAD2000 DXF 0 0";
                        process.Start();
                        process.WaitForExit();// Waits here for the process to exit.                    
                        if (!isInSetting && process.ExitCode == 0) Settings.GlobalSettings.SetValue("DwgDxfConverter", converter);
                    }
                }
                else
                {
                    DxfFileName = fileName;
                }
            }
            private string GetTemporaryDirectory()
            {
                string tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
                Directory.CreateDirectory(tempDirectory);
                return tempDirectory;
            }
            void IDisposable.Dispose()
            {
                if (folderOrg != null)
                {
                    try
                    {
                        if (Directory.Exists(folderOrg))
                            Directory.Delete(folderOrg, true);
                        if (Directory.Exists(folderConverted))
                            Directory.Delete(folderConverted, true);
                    }
                    catch (Exception ex)
                    {
                        if (ex is DirectoryNotFoundException || ex is IOException || ex is UnauthorizedAccessException)
                        {
                            //Best effort, if the folders could not be deleted there is nothing we can do.
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }
        private static Project ImportDXF(string filename)
        {
            using (ConvertToDxfAutoCad2000 converted = new ConvertToDxfAutoCad2000(filename, "dxf"))
            {
                CADability.DXF.Import import = new DXF.Import(converted.DxfFileName);
                return import.Project;
            }
        }
        private static Project ImportDWG(string filename)
        {
            using (ConvertToDxfAutoCad2000 converted = new ConvertToDxfAutoCad2000(filename, "dxf"))
            {
                CADability.DXF.Import import = new DXF.Import(converted.DxfFileName);
                return import.Project;
            }
        }
        public static Project ReadFromFile(string FileName, string Format, bool useProgress, bool makeCompounds = true)
        {
            useProgress = useProgress && !Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false);

            //Settings.GlobalSettings.SetValue("StepImport.ImportOnlyAssembly", false);
            bool onlyAssembly = Settings.GlobalSettings.GetBoolValue("StepImport.ImportOnlyAssembly", false);
            // System.Diagnostics.Trace.WriteLine("ReadFromFile: " + DateTime.Now.ToString());
            Format = Format.ToLower();
            //ProgressFeedBack progressFeedBack = null;
            //if (useProgress)
            //{
            //    progressFeedBack = new ProgressFeedBack();
            //    progressFeedBack.Title = StringTable.GetFormattedString("ReadFile.Progress", FileName);
            //    progressFeedBack.Float(20, 5); // 5 Sekunden bis die Datei gelesen ist, bestimmt die Anfangsgeschwindigkeit
            //}
            switch (Format)
            {
                case "cdb": return ReadFromFile(FileName, useProgress);
                case "dxf": return ImportDXF(FileName);
                case "dwg": return ImportDWG(FileName);
                case "step":
                case "stp":
                    ImportStep importStep = new ImportStep();
                    importStep.HierarchyToBlocks = Settings.GlobalSettings.GetBoolValue("ImportStep.UseHierarchy", true);
                    GeoObjectList list = importStep.Read(FileName);
                    Project project = Project.CreateSimpleProject();
                    foreach (IGeoObject go in list)
                    {
                        if (go is IColorDef icd && icd.ColorDef == null) icd.SetTopLevel(project.ColorList.CreateOrFind("Standard", Color.Red), true);
                        if (go.Layer == null) go.Layer = project.LayerList.CreateOrFind("Standard");
                        AttributeListContainer.UpdateObjectAttrinutes(project, go);
                        go.UpdateAttributes(project);
                    }
                    project.GetActiveModel().Add(list);
                    return project;
                case "brep":
                    break;
                case "stl":
                    ImportSTL importSTL = new ImportSTL();
                    Shell[] shells = importSTL.Read(FileName);
                    if (shells != null)
                    {
                        project = Project.CreateSimpleProject();
                        Model model = project.GetActiveModel();
                        for (int i = 0; i < shells.Length; i++)
                        {
                            project.SetDefaults(shells[i]);
                            if (shells[i].HasOpenEdgesExceptPoles())
                            {
                                model.Add(shells[i]);
                            }
                            else
                            {
                                Solid sld = Solid.Construct();
                                sld.SetShell(shells[i]);
                                project.SetDefaults(sld);
                                model.Add(sld);
                            }
                        }
                        return project;
                    }
                    break;
                case "sat":
                    break;
                case "xt":
                case "x_t":
                    break;
            }
            return null;
        }
        public static Project ReadFromFile(string FileName, string Format)
        {
            return ReadFromFile(FileName, Format, true);
        }
        #region IShowPropertyImpl Overrides
        private IShowProperty[] ShowProperties; // die Anzeige wird hier lokal gehalten, um die TabIndizes setzen zu können
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
                if (ShowProperties == null)
                {
                    ArrayList res = new ArrayList();
                    // 1.: die Attributlisten in der gewünschten Reihenfolge
                    res.Add(attributeLists["ColorList"]);
                    res.Add(attributeLists["LayerList"]);
                    res.Add(attributeLists["LineWidthList"]);
                    res.Add(attributeLists["LinePatternList"]);
                    res.Add(attributeLists["HatchStyleList"]);
                    res.Add(attributeLists["DimensionStyleList"]);
                    res.Add(attributeLists["StyleList"]);
                    foreach (IAttributeList list in attributeLists.Values)
                    {
                        if (list != null)
                        {
                            if (!res.Contains(list)) res.Add(list);
                        }
                    }
                    res.Add(filterList);
                    // die Modelle
                    res.Add(new ModelsProperty(this));
                    //					res.Add(new SeperatorProperty("Project.Models"));
                    //					foreach (Model m in models)
                    //					{
                    //						res.Add(m);
                    //					}
                    // NamedValues
                    res.Add(namedValues);

                    if (UserData != null)
                    {
                        string[] entries = UserData.AllItems;
                        for (int i = 0; i < entries.Length; ++i)
                        {
                            object data = UserData[entries[i]];
                            if (data is IShowProperty)
                            {
                                res.Add(data as IShowProperty);
                            }
                        }
                    }

                    ShowProperties = res.ToArray(typeof(IShowProperty)) as IShowProperty[];
                }
                return ShowProperties;
            }
        }
        #endregion
        public delegate void RefreshDelegate(object sender, EventArgs args);
        public event RefreshDelegate RefreshEvent;
        public delegate void ViewChangedDelegate(Project sender, IView viewWhichChanged);
        public event ViewChangedDelegate ViewChangedEvent;
        public delegate void ModelsChangedDelegate(Project sender, Model model, bool added);
        public event ModelsChangedDelegate ModelsChangedEvent;
        internal static void InsertDebugLine(GeoPoint sp, GeoPoint ep, Color clr)
        {
            IFrame fr = FrameImpl.MainFrame;
            Model m = fr.Project.GetActiveModel();
            Line l = Line.Construct();
            l.SetTwoPoints(sp, ep);
            ColorDef cd = new ColorDef("Debug:" + clr.ToString(), clr);
            l.ColorDef = cd;
            m.Add(l);
        }
        internal static void InsertDebugLine(GeoPoint2D sp, GeoPoint2D ep, Color clr)
        {
            IFrame fr = FrameImpl.MainFrame;
            Model m = fr.Project.GetActiveModel();
            Line l = Line.Construct();
            l.SetTwoPoints(Plane.XYPlane.ToGlobal(sp), Plane.XYPlane.ToGlobal(ep));
            ColorDef cd = new ColorDef("Debug:" + clr.ToString(), clr);
            l.ColorDef = cd;
            m.Add(l);
        }
        internal static void InsertDebugCurve2D(CADability.Curve2D.ICurve2D c2d, Color clr)
        {
            IFrame fr = FrameImpl.MainFrame;
            Model m = fr.Project.GetActiveModel();
            IGeoObject go = c2d.MakeGeoObject(Plane.XYPlane);
            ColorDef cd = new ColorDef("Debug:" + clr.ToString(), clr);
            (go as IColorDef).ColorDef = cd;
            m.Add(go);
        }
        #region IAttributeListContainer
        IAttributeList IAttributeListContainer.GetList(string keyName)
        {
            return attributeLists[keyName] as IAttributeList;
        }
        int IAttributeListContainer.ListCount
        {
            get
            {
                return attributeLists.Count;
            }
        }
        IAttributeList IAttributeListContainer.List(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= attributeLists.Count) return null;
            int i = 0;
            foreach (IAttributeList list in attributeLists.Values)
            {
                if (i == keyIndex)
                    return list;
                i++;
            }
            return null;
        }
        string IAttributeListContainer.ListKeyName(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= attributeLists.Count) return null;
            int i = 0;
            foreach (string keyName in attributeLists.Keys)
            {
                if (i == keyIndex)
                    return keyName;
                i++;
            }
            return null;
        }
        void IAttributeListContainer.Add(string KeyName, IAttributeList ToAdd)
        {
            if (attributeLists.ContainsKey(KeyName))
                throw new AttributeException("KeyName already exists in Project", AttributeException.AttributeExceptionType.InvalidArg);
            if (ToAdd != null)
            {
                attributeLists.Add(KeyName, ToAdd);
                ToAdd.Owner = this;
            }
        }
        void IAttributeListContainer.Remove(string KeyName)
        {
            switch (KeyName)
            {
                case "ColorList":
                case "LayerList":
                case "HatchStyleList":
                case "DimensionStyleList":
                case "LineWidthList":
                case "LinePatternList":
                case "StyleList":
                    throw new AttributeException("Not allowed to remove " + KeyName + "from Project", AttributeException.AttributeExceptionType.InvalidArg);
                default:
                    attributeLists.Remove(KeyName);
                    break;
            }

        }


        public ColorList ColorList
        {
            get
            {
                return attributeLists["ColorList"] as ColorList;
            }
        }
        public LayerList LayerList
        {
            get
            {
                return attributeLists["LayerList"] as LayerList;
            }
        }
        public HatchStyleList HatchStyleList
        {
            get
            {
                return attributeLists["HatchStyleList"] as HatchStyleList;
            }
        }
        public DimensionStyleList DimensionStyleList
        {
            get
            {
                return attributeLists["DimensionStyleList"] as DimensionStyleList;
            }
        }
        public LineWidthList LineWidthList
        {
            get
            {
                return attributeLists["LineWidthList"] as LineWidthList;
            }
        }
        public LinePatternList LinePatternList
        {
            get
            {
                return attributeLists["LinePatternList"] as LinePatternList;
            }
        }
        public StyleList StyleList
        {
            get
            {
                return attributeLists["StyleList"] as StyleList;
            }
        }
        void IAttributeListContainer.AttributeChanged(IAttributeList list, INamedAttribute attribute, ReversibleChange change)
        {
            // TODO: über alle GeoObjekte gehen und AttributChanged aufrufen
            // ähnlich zu OnRemovingAttribute
            // Die Bemaßungen und Schraffuren sollen jedenfalls needsRecalc setzen
            // wenn sie betroffen sind
            // ungeklärt ist noch wie mit SetFocus/LostFocus im Controlcenter umgegangen werden soll
            // um nicht wärend des Eintippens ständige Bildaufbauten zu haben
            // Ebenso ist noch unklar, wie das in die UndoListe kommt...
            // ob folgendes den Erfordernissen entspricht ist noch nicht klar:
            if (!change.IsMethod("Add") && !change.IsMethod("Remove"))
            {
                bool needsRefresh = false;
                foreach (Model m in models)
                {
                    for (int i = 0; i < m.Count; ++i)
                    {
                        needsRefresh |= m[i].AttributeChanged(attribute);
                    }
                }
                if (needsRefresh && !deferRefresh && RefreshEvent != null) RefreshEvent(this, null);
            }
        }
        bool IAttributeListContainer.RemovingItem(IAttributeList list, INamedAttribute attribute, string resourceId)
        {	// wird aufgerufen, wenn eine Liste ein Item entfernen möchte
            if (list.Count < 2)
            {
                Frame.UIService.ShowMessageBox(StringTable.GetString(resourceId + ".DontRemoveLastItem"),
                StringTable.GetString(resourceId + ".Label"), MessageBoxButtons.OK);
                return false; // nicht entfernen
            }
            bool used = false;
            foreach (Model m in models)
            {
                if (m.IsAttributeUsed(attribute))
                {
                    used = true;
                    break;
                }
            }
            if (used)
            {
                if (!Settings.GlobalSettings.GetBoolValue("DontUse.WindowsForms", false))
                {
                    if (DialogResult.No == Frame.UIService.ShowMessageBox(StringTable.GetFormattedString(resourceId + ".ItemIsUsed", attribute.Name), StringTable.GetString(resourceId + ".Label"), MessageBoxButtons.YesNo))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        void IAttributeListContainer.UpdateList(IAttributeList list)
        {
            foreach (Model m in models)
                for (int i = 0; i < m.Count; i++)
                {
                    INamedAttribute[] attributes = m[i].Attributes;
                    foreach (INamedAttribute attr in attributes)
                        list.Add(attr);
                }
        }

        #endregion
        private bool deferRefresh;
        public bool DeferRefresh
        {
            get { return deferRefresh; }
            set
            {
                // die Auswirkungen auf den PaintBuffer über die verschiedensten Umwege
                // müssen noch geklärt werden
                if (value == false && deferRefresh)
                {	// ausschalten
                    if (RefreshEvent != null) RefreshEvent(this, null);
                }
                deferRefresh = value;
            }
        }
        public void SetDefaults(IGeoObject go)
        {
            Style style = this.StyleList.GetDefault(go.PreferredStyle);
            if (style != null)
            {
                go.Style = style;
            }
            // wenn der Stil gesetzt ist sind manche Attribute gesetzt, andere sind noch null
            // letztere werden noch mit dem aktuellen gesetzt
            LayerList layerList = LayerList;
            if (layerList.Current != null && go.Layer == null)
                go.Layer = layerList.Current;
            IColorDef clr = go as IColorDef;
            if (clr != null && clr.ColorDef == null)
                clr.ColorDef = ColorList.Current;
            IDimensionStyle ds = go as IDimensionStyle;
            if (ds != null && ds.DimensionStyle == null)
            {
                ds.DimensionStyle = DimensionStyleList.Current;
            }
            ILineWidth lw = go as ILineWidth;
            if (lw != null && lw.LineWidth == null)
            {
                lw.LineWidth = LineWidthList.Current;
            }
            ILinePattern lp = go as ILinePattern;
            if (lp != null && lp.LinePattern == null)
            {
                lp.LinePattern = LinePatternList.Current;
            }
            IHatchStyle hs = go as IHatchStyle;
            if (hs != null && hs.HatchStyle == null)
            {
                hs.HatchStyle = HatchStyleList.Current;
            }
        }
        public static void SerializeObject(ISerializable toSerialize, string fileName)
        {
            Stream stream = File.Open(fileName, System.IO.FileMode.Create);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, toSerialize);
            stream.Close();
            stream.Dispose();
        }
        public static object DeserializeObject(string fileName)
        {
            Stream stream = File.Open(fileName, System.IO.FileMode.Open);
            BinaryFormatter formatter = new BinaryFormatter();
            object res = formatter.Deserialize(stream);
            stream.Close();
            stream.Dispose();
            return res;
        }
#if DEBUG
        /// <summary>
        /// Probleme beim Serialisieren (z.B. Datenverlust) können hiermit getestet werden. Es wird in einen MemoryStream srialisiert
        /// und gleich wieder deserialisiert. Das zurückgelieferte Objekt hat keine verbindung mehr zu anderen Objekten
        /// </summary>
        /// <param name="toSerialize"></param>
        /// <returns></returns>
        public static object SerializeDeserialize(object toSerialize)
        {
            Stream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File, null));
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            // formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString; // XsdString macht das wechseln von .NET Frameworks schwierig
            formatter.Serialize(stream, toSerialize);
            stream.Seek(0, SeekOrigin.Begin);
            formatter = new BinaryFormatter();
            return formatter.Deserialize(stream);

        }
#endif
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Project(SerializationInfo info, StreamingContext context)
        {
            models = (ArrayList)info.GetValue("Models", typeof(ArrayList));
            activeModelIndex = (int)info.GetValue("ActiveModelIndex", typeof(int));
            attributeLists = (Hashtable)InfoReader.Read(info, "AttributeLists", typeof(Hashtable));
            UserData = (UserData)info.GetValue("UserData", typeof(UserData));
            UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataRemoved);
            symbolList = InfoReader.ReadOrCreate(info, "SymbolList", typeof(GeoObjectList), new object[] { }) as GeoObjectList;
#if !WEBASSEMBLY
            try
            {
                object ll = info.GetValue("Layouts", typeof(object));
                if (ll is Layout[])
                {
                    ser_layouts = ll as Layout[];
                    ser_layoutlist = null;
                }
                else if (ll is ArrayList)
                {
                    ser_layoutlist = ll as ArrayList;
                    ser_layouts = null;
                }
            }
            catch (SerializationException)
            {
                ser_layouts = new Layout[0]; // es gibt keine Layouts
                ser_layoutlist = null;
            }
#endif
            ser_modelViews = null;
            try
            {
                ser_projectedModels = (ProjectedModel[])info.GetValue("ProjectedModels", typeof(ProjectedModel[]));
            }
            catch (SerializationException)
            {
                ser_modelViews = InfoReader.ReadOrCreate(info, "ModelViews", typeof(ArrayList), new object[] { }) as ArrayList;
            }
            // ansonsten alte Daten in OnDeserialisationDone übernehmen
            namedValues = InfoReader.ReadOrCreate(info, "NamedValues", typeof(NamedValuesProperty)) as NamedValuesProperty;
            filterList = InfoReader.ReadOrCreate(info, "FilterList", typeof(FilterList)) as FilterList;
            try
            {
                ser_animatedViews = info.GetValue("AnimatedViews", typeof(AnimatedView[])) as AnimatedView[];
            }
            catch (SerializationException)
            {
            }
#if !WEBASSEMBLY
            try
            {
                ser_gdiViews = info.GetValue("GdiViews", typeof(GDI2DView[])) as GDI2DView[];
            }
            catch (SerializationException)
            {
            }
#endif
            try
            {
                activeViewName = info.GetString("ActiveViewName");
            }
            catch (SerializationException)
            {
            }
            if (info.MemberCount > 12) // to avoid exceptions
            {
                try
                {
                    PageSettings ps = (PageSettings)info.GetValue("DefaultPageSettings", typeof(PageSettings));
                    if (printDocument == null) printDocument = new PrintDocument();
                    printDocument.DefaultPageSettings = ps;
                }
                catch (SerializationException)
                {
                }
            }
            base.resourceId = "ProjectSettings";
        }
        void OnUserDataRemoved(string name, object value)
        {
            if (propertyTreeView != null)
            {
                ShowProperties = null;
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        void OnUserDataAdded(string name, object value)
        {
            if (propertyTreeView != null)
            {
                ShowProperties = null;
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        //private void UpdateModelViews()
        //{
        //    foreach (ModelViewDescription mvd in modelViews)
        //    {
        //        mvd.VisibleLayers = new ArrayList();
        //        foreach (Layer l in LayerList)
        //        {
        //            if (mvd.GetModelView(null).ProjectedModel.IsLayerVisible(l))
        //            {
        //                mvd.VisibleLayers.Add(l);
        //            }
        //        }
        //    }
        //}
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Models", models);
            info.AddValue("ActiveModelIndex", activeModelIndex);
            info.AddValue("AttributeLists", attributeLists);
            info.AddValue("UserData", UserData);
            info.AddValue("SymbolList", symbolList);
#if !WEBASSEMBLY
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                info.AddValue("Layouts", layouts.ToArray()); // Generics erwarten die exakte Version beim Deserialisieren, geht also nicht
                info.AddValue("GdiViews", gdiViews.ToArray());
            }
#endif
            info.AddValue("ProjectedModels", projectedModels.ToArray());
            info.AddValue("NamedValues", namedValues);
            info.AddValue("FilterList", filterList);
            info.AddValue("AnimatedViews", animatedViews.ToArray());
            info.AddValue("ActiveViewName", activeViewName);
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                if (printDocument != null) info.AddValue("DefaultPageSettings", printDocument.DefaultPageSettings);
            }
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Models", models);
            data.AddProperty("ActiveModelIndex", activeModelIndex);
            data.AddProperty("AttributeLists", attributeLists);
            data.AddProperty("UserData", UserData);
            data.AddProperty("SymbolList", symbolList);
#if !WEBASSEMBLY
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                data.AddProperty("Layouts", layouts);
                data.AddProperty("GdiViews", gdiViews);
            }
#endif
            data.AddProperty("ProjectedModels", projectedModels);
            data.AddProperty("NamedValues", namedValues);
            data.AddProperty("FilterList", filterList);
            data.AddProperty("AnimatedViews", animatedViews);
            data.AddProperty("ActiveViewName", activeViewName);
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                if (printDocument != null) data.AddProperty("DefaultPageSettings", printDocument.DefaultPageSettings);
            }
        }

        public void SetObjectData(IJsonReadData data)
        {
            int version = data.Version;
            models = new ArrayList(data.GetPropertyOrDefault<List<Model>>("Models"));
            activeModelIndex = data.GetPropertyOrDefault<int>("ActiveModelIndex");
            attributeLists = data.GetPropertyOrDefault<Hashtable>("AttributeLists");
            UserData = data.GetPropertyOrDefault<UserData>("UserData");
            symbolList = data.GetPropertyOrDefault<GeoObjectList>("SymbolList");
#if !WEBASSEMBLY
            try
            {
                if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
                {
                    layouts = data.GetPropertyOrDefault<List<Layout>>("Layouts");
                    gdiViews = data.GetPropertyOrDefault<List<GDI2DView>>("GdiViews");
                }
            }
            catch (PlatformNotSupportedException) { }
#endif
            projectedModels = data.GetPropertyOrDefault<List<ProjectedModel>>("ProjectedModels");
            namedValues = data.GetPropertyOrDefault<NamedValuesProperty>("NamedValues");
            filterList = data.GetPropertyOrDefault<FilterList>("FilterList");
            animatedViews = data.GetPropertyOrDefault<List<AnimatedView>>("AnimatedViews");
            activeViewName = data.GetPropertyOrDefault<string>("ActiveViewName");
            try
            {
                if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
                {
                    printDocument = new PrintDocument();
                    printDocument.DefaultPageSettings = data.GetPropertyOrDefault<PageSettings>("DefaultPageSettings");
                }
            }
            catch (PlatformNotSupportedException) { }
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (IAttributeList list in attributeLists.Values)
                list.Owner = this;
            if (ColorList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "ColorList", true);
            }
            if (LayerList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "LayerList", true);
            }
            if (HatchStyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "HatchStyleList", true);
            }
            if (DimensionStyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "DimensionStyleList", true);
            }
            if (StyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "StyleList", true);
            }
            AttributeListContainer.UpdateLists(this, true);
            filterList.AttributeListContainer = this;

            undoRedoSystem = new UndoRedoSystem();
            for (int i = 0; i < models.Count; ++i)
            {
                Model m = models[i] as Model;
                m.Undo = undoRedoSystem;
                m.GeoObjectAddedEvent += new Model.GeoObjectAdded(OnGeoObjectAddedToModel);
                m.GeoObjectRemovedEvent += new Model.GeoObjectRemoved(OnGeoObjectRemovedFromModel);
                m.GeoObjectDidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
                m.NameChangedEvent += new Model.NameChangedDelegate(OnModelNameChanged);
            }
#if !WEBASSEMBLY
            if (layouts == null) layouts = new List<Layout>();
            foreach (Layout l in layouts)
            {
                l.project = this;
            }
#endif
#if DEBUG
            IShowProperty[] dbg = namedValues.SubEntries;
#endif
        }
        #endregion
        #region ICommandHandler Members
#if !WEBASSEMBLY
        private void OnNewLayout()
        {
            int i = 1;
            string name = StringTable.GetFormattedString("Layout.Default.Name", i);
            while (this.FindLayoutName(name))
            {
                ++i;
                name = StringTable.GetFormattedString("Layout.Default.Name", i);
            }
            Layout l = new Layout(this);
            l.Name = name;
            l.PaperWidth = 297;
            l.PaperHeight = 210;
            Border bdr = Border.MakeRectangle(10.0, 287, 10.0, 200); // 10mm Rand
            Projection pr = new Projection(Projection.StandardProjection.FromTop);
            Model m = models[0] as Model;
            BoundingRect ext = m.GetExtent(pr);
            double f = 1.0;
            while (f * ext.Width > l.PaperWidth || f * ext.Height > l.PaperHeight)
            {	// kleiner machen
                f = f / 10.0;
            }
            ModOp scl = ModOp.Scale(f);
            ModOp trn = ModOp.Translate(l.PaperWidth / 2.0 - f * ext.Width / 2.0, l.PaperHeight / 2.0 - f * ext.Height / 2.0, 0.0);
            // pr.PrependModOp(trn*scl);
            pr.SetPlacement(f, l.PaperWidth / 2.0 - f * ext.Width / 2.0, l.PaperHeight / 2.0 - f * ext.Height / 2.0);
            int ind = this.AddLayout(l);
            if (propertyTreeView != null)
            {
                ShowProperties = null;
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
            if (ViewChangedEvent != null) ViewChangedEvent(this, null);
        }
#endif
        private string GetNewModelViewName()
        {
            int i = 1;
            string name = StringTable.GetFormattedString("ModelView.Default.Name", i);
            while (FindModelViewName(name))
            {
                ++i;
                name = StringTable.GetFormattedString("ModelView.Default.Name", i);
            }
            return name;
        }
#if !WEBASSEMBLY
        internal string GetNewGDI2DViewName()
        {
            int i = 1;
            string name = StringTable.GetFormattedString("GDI2DView.Default.Name", i);
            bool found = false;
            do
            {
                found = false;
                for (int j = 0; j < gdiViews.Count; j++)
                {
                    if (gdiViews[j].Name == name)
                    {
                        found = true;
                        ++i;
                        name = StringTable.GetFormattedString("GDI2DView.Default.Name", i);
                        break;
                    }
                }
            } while (found);
            return name;
        }
#endif
        internal string GetNewAnimatedViewName()
        {
            int i = 1;
            string name = StringTable.GetFormattedString("AnimatedView.Default.Name", i);
            bool found = false;
            do
            {
                found = false;
                for (int j = 0; j < animatedViews.Count; j++)
                {
                    if (animatedViews[j].Name == name)
                    {
                        found = true;
                        ++i;
                        name = StringTable.GetFormattedString("GDI2DView.Default.Name", i);
                        break;
                    }
                }
            } while (found);
            return name;
        }
        private void OnNewModelView()
        {
            OnNewModelView(this.GetActiveModel());
        }
        private void OnNewModelView(Model m)
        {
            string name = GetNewModelViewName();
            int ind = AddProjectedModel(name, m, new Projection(Projection.StandardProjection.FromTop));
            // komisch: die ProjectedModel und Layouts stehen nicht beim Projekt, spielen also hier keine Rolle
            // folgendes ist also unnötig:
            if (propertyTreeView != null)
            {
                ShowProperties = null;
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
            if (ViewChangedEvent != null) ViewChangedEvent(this, null);
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Edit.Undo":
                    undoRedoSystem.UndoLastStep();
                    return true;
                case "MenuId.Edit.Redo":
                    undoRedoSystem.RedoLastStep();
                    return true;
                case "MenuId.View.Model.NewModelView":
                    OnNewModelView();
                    return true;
                case "MenuId.View.NewAnimatedView":
                case "MenuId.View.NewGDIView":
                    if (base.Frame != null)
                    {
                        (Frame as ICommandHandler).OnCommand(MenuId); // wird dort ausgeführt
                    }
                    return true;
#if !WEBASSEMBLY
                case "MenuId.View.Layout.NewLayout":
                    OnNewLayout();
                    return true;
#endif
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Edit.Undo":
                    CommandState.Enabled = undoRedoSystem.CanUndo();
                    return true;
                case "MenuId.Edit.Redo":
                    CommandState.Enabled = undoRedoSystem.CanRedo();
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region IEnumerable
        public void Add(object toAdd)
        {
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return models.GetEnumerator();
        }
        #endregion
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            attributeLists.OnDeserialization(sender);
            foreach (IAttributeList list in attributeLists.Values)
                list.Owner = this;
            // hierhin übertragen von protected Project(SerializationInfo info, StreamingContext context)
            // da im Konstruktor attributeLists noch nicht vollständig eingelesen ist.
            if (ColorList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "ColorList", true);
            }
            else
            {
                (ColorList as IDeserializationCallback).OnDeserialization(sender);
            }
            if (LayerList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "LayerList", true);
            }
            else
            {
                (LayerList as IDeserializationCallback).OnDeserialization(sender);
            }
            if (HatchStyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "HatchStyleList", true);
            }
            else
            {
                (HatchStyleList as IDeserializationCallback).OnDeserialization(sender);
            }
            if (DimensionStyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "DimensionStyleList", true);
            }
            else
            {
                (DimensionStyleList as IDeserializationCallback).OnDeserialization(sender);
            }
            if (StyleList == null)
            {
                AttributeListContainer.CloneAttributeList(Settings.GlobalSettings, this, "StyleList", true);
            }
            else
            {
                (StyleList as IDeserializationCallback).OnDeserialization(sender);
            }
            // Problem: die OnDeserialization für die Listen müssen vorher aufgerufen werden
            // wie kann man das erzwingen? zunächst mal wird's jetzt einfach vorher aufgerufen, auf die Gefahr hin, dass
            // es zweimal aufgerufen wird.
            // AttributeListContainer.UpdateLists(this, true);
            filterList.AttributeListContainer = this;

            undoRedoSystem = new UndoRedoSystem();
            for (int i = 0; i < models.Count; ++i)
            {
                Model m = models[i] as Model;
                m.Undo = undoRedoSystem;
                m.GeoObjectAddedEvent += new Model.GeoObjectAdded(OnGeoObjectAddedToModel);
                m.GeoObjectRemovedEvent += new Model.GeoObjectRemoved(OnGeoObjectRemovedFromModel);
                m.GeoObjectDidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
                m.NameChangedEvent += new Model.NameChangedDelegate(OnModelNameChanged);
            }
#if !WEBASSEMBLY
            if (ser_layouts != null)
            {
                layouts = new List<Layout>(ser_layouts);
            }
            else if (ser_layoutlist != null)
            {
                layouts = new List<Layout>();
                layouts.AddRange((Layout[])ser_layoutlist.ToArray(typeof(Layout)));
            }
            foreach (Layout l in layouts)
            {
                l.project = this;
            }
#endif
            if (ser_projectedModels != null)
            {
                projectedModels = new List<ProjectedModel>(ser_projectedModels);
            }
            else if (ser_modelViews != null)
            {
                projectedModels = new List<ProjectedModel>();
                foreach (ModelViewDescription mvd in ser_modelViews)
                {
                    ProjectedModel pm = new ProjectedModel(mvd.Model, mvd.Projection);
                    pm.Name = mvd.Name;
                    projectedModels.Add(pm);
                }
            }
            if (ser_animatedViews != null)
            {
                animatedViews = new List<AnimatedView>(ser_animatedViews);
                ser_animatedViews = null;
            }
            else
            {
                animatedViews = new List<AnimatedView>();
            }
#if !WEBASSEMBLY
            if (ser_gdiViews != null)
            {
                gdiViews = new List<GDI2DView>(ser_gdiViews);
                ser_gdiViews = null;
            }
            else
            {
                gdiViews = new List<GDI2DView>();
            }
#endif
        }

        #endregion
    }
}
