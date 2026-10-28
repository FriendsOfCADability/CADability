using CADability.Attribute;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.Substitutes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Point = System.Drawing.Point;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ACadSharp;
using ACadSharp.IO;
using Color = System.Drawing.Color;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
// using System.Runtime.Serialization.Formatters.Soap; Müsste man mal einführen, referenz muss verwendet werden


namespace CADability
{

    /// <summary>
    /// Exception thrown by methods of <see cref="Project"/> when they are called with invalid arguments, e.g.
    /// <see cref="Project.SetActiveModel(int)"/> with an index that is out of range. <see cref="ExceptionType"/> tells
    /// the reason.
    /// </summary>
    public class ProjectException : ApplicationException
    {
        /// <summary>
        /// The reasons for a <see cref="ProjectException"/>: <see cref="tExceptionType.InvalidModelIndex"/> means that a
        /// model index is out of range.
        /// </summary>
        public enum tExceptionType { InvalidModelIndex };
        /// <summary>
        /// The reason for the exception.
        /// </summary>
        public tExceptionType ExceptionType;
        /// <summary>
        /// Creates the exception with the given reason.
        /// </summary>
        /// <param name="ExceptionType">The reason for the exception</param>
        public ProjectException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// Exception for errors when a project file is read, e.g. a file written by a newer version of CADability.
    /// <see cref="ResourceId"/> names the entry of the <see cref="StringTable"/> with the message for the user.
    /// <see cref="Project.ReadFromFile(string)"/> catches it and returns null. The current code does not throw it itself,
    /// it remains for projects or deserialization constructors of user objects that do.
    /// </summary>
    public class ProjectSerializationException : SerializationException
    {
        private string resourceId; // resourceId für die MessageBox
        /// <summary>
        /// Creates the exception.
        /// </summary>
        /// <param name="resourceId">The id of the message for the user in the <see cref="StringTable"/>, may be null</param>
        public ProjectSerializationException(string resourceId)
            : base()
        {
            this.resourceId = resourceId;
        }
        /// <summary>
        /// The id of the message for the user in the <see cref="StringTable"/> (see <see cref="StringTable.GetString(string)"/>),
        /// or null if there is none.
        /// </summary>
        public string ResourceId
        {
            get { return resourceId; }
        }
    }
    internal class ProjectOldVersionException : ApplicationException
    {
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
        /// <summary>
        /// Delegate for <see cref="Constructor"/>: creates a new, empty project, usually an object of a class derived from
        /// <see cref="Project"/>.
        /// </summary>
        /// <returns>The new project</returns>
        public delegate Project ConstructionDelegate();
        /// <summary>
        /// Hook for the polymorphic construction of projects. When set, <see cref="Construct"/> calls this delegate instead of
        /// creating a plain <see cref="Project"/>, so that an application can substitute its own class derived from Project
        /// wherever CADability creates a project by <see cref="Construct"/> (e.g. <see cref="CreateSimpleProject"/>).
        /// Null by default.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// Creates a new, empty project: by <see cref="Constructor"/> if that is set, otherwise a plain <see cref="Project"/>.
        /// Use it instead of the constructor when an application may have substituted a derived class. The project has no
        /// model yet; <see cref="CreateSimpleProject"/> creates a project with a model and the standard views.
        /// </summary>
        /// <returns>The new project</returns>
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
        private List<GDI2DView> gdiViews; // die AnimatedViews
        private List<Layout> layouts; // die zugehörigen Layouts
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
            /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
            /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
            /// </summary>
            /// <param name="info">receives the data</param>
            /// <param name="context">the destination of the serialization</param>
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
        private UndoRedoSystem undoRedoSystem;
        private GeoObjectList symbolList;
        private NamedValuesProperty namedValues;

        // Daten nur um zu deserialisieren von älteren Versionen
        private ArrayList ser_layoutlist;
        private ArrayList ser_modelViews;
        private ProjectedModel[] ser_projectedModels;
        private AnimatedView[] ser_animatedViews;
        private Layout[] ser_layouts;
        private GDI2DView[] ser_gdiViews;
        private PrintDocument printDocument; // das PrintDocument zum Drucken, enthält die Druckereinstellungen; created on demand (Windows)
        private PageSetup defaultPageSetup; // the page settings as long as there is no printDocument
        /// <summary>
        /// The page settings of the project for printing, which are saved with the project. Null if none have been set: then
        /// the printer's defaults are used. On Windows they are also changed by the page setup and printer dialogs.
        /// </summary>
        public PageSetup DefaultPageSetup
        {
            get
            {
                if (printDocument != null)
                {
                    try
                    {
                        return PageSetup.FromPageSettings(printDocument.DefaultPageSettings);
                    }
                    catch (InvalidPrinterException) { } // no printer installed: the settings that were read or set
                }
                return defaultPageSetup?.Clone();
            }
            set
            {
                defaultPageSetup = value?.Clone();
                printDocument = null; // made again from the new settings when needed
            }
        }
        /// <summary>
        /// The print document with the page settings of the project, created from <see cref="DefaultPageSetup"/> when it is
        /// first needed (Windows only).
        /// </summary>
        internal PrintDocument PrintDocument
        {
            get
            {
                if (printDocument == null)
                {
                    PrintDocument created = new PrintDocument();
                    try
                    {
                        defaultPageSetup?.ApplyTo(created);
                    }
                    catch (InvalidPrinterException) { } // no printer installed: its defaults
                    printDocument = created;
                }
                return printDocument;
            }
        }
        /// <summary>
        /// Creates an empty Project. The empty project contains clones of the globally defined
        /// attributes and attribute lists (like colors, layers etc.)
        /// </summary>
        protected Project()
        {
            models = new ArrayList();
            attributeLists = new Hashtable();
            activeModelIndex = -1;

            layouts = new List<Layout>();
            gdiViews = new List<GDI2DView>();
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
            base.resourceIdInternal = "ProjectSettings";

            UserData = new UserData();
            UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataRemoved);
            undoRedoSystem = new UndoRedoSystem();

            symbolList = new GeoObjectList();
            namedValues = new NamedValuesProperty();
        }
        /// <summary>
        /// Finalizer. It does nothing; a project holds no unmanaged resources.
        /// </summary>
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
            IsModified = true;
        }
        private void OnGeoObjectRemovedFromModel(IGeoObject go)
        {
            IsModified = true;
        }
        private void OnGeoObjectAddedToModel(IGeoObject go)
        {
            IsModified = true;
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
        /// <summary>
        /// Returns the number of model views (<see cref="ModelView"/>) in this project
        /// </summary>
        internal int ModelViewCount
        {
            get
            {
                return projectedModels.Count;
            }
        }
        /// <summary>
        /// The <see cref="AnimatedView"/>s defined in this project. The list is saved with the project.
        /// <see cref="FrameImpl.AddView(IView)"/> and <see cref="FrameImpl.RemoveView(IView)"/> add views to and remove them
        /// from this list and also register them with the frame; the list may be modified directly, but the frame does not
        /// notice that.
        /// </summary>
        public List<AnimatedView> AnimatedViews
        {
            get
            {
                return animatedViews;
            }
        }
        /// <summary>
        /// Gets a list of all defined GDI2DViews in this project.
        /// </summary>
        public List<GDI2DView> GdiViews
        {
            get
            {
                if (gdiViews == null) { gdiViews = new List<GDI2DView>(); }
                return gdiViews;
            }
        }
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
        internal bool FindLayoutName(string name)
        {
            foreach (Layout l in layouts)
            {
                if (l.Name == name) return true;
            }
            return false;
        }
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
        /// <summary>
        /// Adds a new <see cref="ProjectedModel"/>, the persistent part of a <see cref="ModelView"/> (the model, the
        /// projection and the visible layers), with the given name to the project. The name should be unique among the model
        /// views of the project, because the frame and the project refer to model views by name. No ModelView is created and
        /// no <see cref="ViewChangedEvent"/> is raised; <see cref="FrameImpl.AddView(IView)"/> calls this when a ModelView is
        /// added to the frame.
        /// </summary>
        /// <param name="name">The name of the model view</param>
        /// <param name="model">The model that is shown</param>
        /// <param name="projection">The projection; it is used as is, not cloned</param>
        /// <returns>The index of the new entry in the list of projected models of the project</returns>
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
        /// <summary>
        /// Removes the <see cref="ProjectedModel"/> with the given index from the list of model views of the project, without
        /// any checks: unlike <see cref="RemoveModelView(ModelView)"/> it also removes the last one and raises no
        /// <see cref="ViewChangedEvent"/>.
        /// </summary>
        /// <param name="index">The index of the projected model, as returned by
        /// <see cref="AddProjectedModel(string, Model, Projection)"/></param>
        public void RemoveModelView(int index)
        {
            projectedModels.RemoveAt(index);
            //modelViews.RemoveAt(index);
        }
        // hier fehlen noch die Add Methoden um neue Layouts und Modelviews zuzufügen
        internal void AssureViews()
        {	// stellt sicher, dass es wenigstens einen ModelView und einen LayoutView gibt
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
            layouts.Clear();
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
        /// <summary>
        /// The same list as <see cref="SymbolList"/>.
        /// </summary>
        public GeoObjectList AdditionalSymbols { get { return symbolList; } }
        /// <summary>
        /// The <see cref="CADability.UserData"/> object can take any kind of data. If the data objects
        /// are serializable i.e. implement <see cref="System.Runtime.Serialization.ISerializable"/> they are serialized
        /// together with the project. If they implement the <see cref="IPropertyEntry"/> interface
        /// they are displayed and can be modified on the project tab page of the control center.
        /// </summary>
        public UserData UserData;
        /// <summary>
        /// The <see cref="CADability.Attribute.FilterList"/> of this project: a list of filters that decide which objects
        /// may be selected or snapped to by the actions (see <see cref="CADability.Actions.SelectObjectsAction"/>). The actions
        /// pass it to <see cref="Model.GetObjectsFromRect(BoundingRect, Projection, HashSet{Layer}, PickMode, FilterList)"/>
        /// and to the <see cref="SnapPointFinder"/>. It is shown in the control center and saved with the project.
        /// </summary>
        public FilterList FilterList
        {
            get
            {
                return filterList;
            }
        }
        /// <summary>
        /// A list of objects (symbols) that are saved with the project but do not belong to any model, e.g. templates for
        /// objects to insert. CADability itself only keeps and serializes the list; the application decides what to put into it.
        /// </summary>
        public GeoObjectList SymbolList
        {
            get
            {
                return symbolList;
            }
        }
        /// <summary>
        /// Returns the named value with the given name. Named values are user defined doubles, <see cref="GeoPoint"/>s and
        /// <see cref="GeoVector"/>s with a name each, which can be referred to by name in input fields and formulas. They are
        /// shown and edited in the "named values" group of the project in the control center and saved with the project.
        /// </summary>
        /// <param name="name">The name of the value</param>
        /// <returns>The value, or null if there is no value with this name</returns>
        public object GetNamedValue(string name)
        {
            return namedValues.GetNamedValue(name);
        }
        /// <summary>
        /// Sets or adds the named value with the given name (see <see cref="GetNamedValue(string)"/>). With <paramref name="val"/>
        /// null the value is removed. With <paramref name="name"/> null a unique name is generated from the type of the
        /// value and, if the project is shown in the control center, the user is prompted to edit that name.
        /// </summary>
        /// <param name="name">The name of the value, or null to generate one</param>
        /// <param name="val">The value (a double, <see cref="GeoPoint"/> or <see cref="GeoVector"/>), or null to remove the entry</param>
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
            set
            {
                isModified = value;
                if (isModified)
                {
                    ProjectModifiedEvent?.Invoke(this);
                }
            }
        }
        /// <summary>
        /// The name of the file in which this project ist stored. May be null.
        /// </summary>
        public string FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }
        /// <summary>
        /// Makes the model with the given index (see <see cref="GetModel(int)"/>) the active model of the project, which
        /// <see cref="GetActiveModel"/> returns: the model that new views show and that the imports and the methods working
        /// without a view (e.g. <see cref="ExportToWebGl(string)"/>) use.
        /// </summary>
        /// <param name="Index">The index of the model</param>
        /// <exception cref="ProjectException">if the index is out of range
        /// (<see cref="ProjectException.tExceptionType.InvalidModelIndex"/>)</exception>
        public void SetActiveModel(int Index)
        {
            if (Index < 0 || Index >= models.Count) throw new ProjectException(ProjectException.tExceptionType.InvalidModelIndex);
            activeModelIndex = Index;
        }
        /// <summary>
        /// Creates a <see cref="ModelView"/> that shows the given model in the standard projection, i.e. the view from top with
        /// the x/y plane as drawing plane. Note that a new view is created on each call; views already defined for the model
        /// are not looked up.
        /// </summary>
        /// <param name="m">The model to show</param>
        /// <returns>The new model view</returns>
        public ModelView GetStandardModelView(Model m)
        {
            // noch nicht richtig implementiert
            ModelView view = new ModelView(this);
            view.ProjectedModel = new ProjectedModel(m, new Projection(Projection.StandardProjection.FromTop));
            view.Projection.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            return view;
        }
        /// <summary>
        /// Creates a <see cref="ModelView"/> that shows the active model (<see cref="GetActiveModel"/>) in the standard
        /// projection, i.e. the view from top with the x/y plane as drawing plane. Like
        /// <see cref="GetStandardModelView(Model)"/> it creates a new view on each call.
        /// </summary>
        /// <returns>The new model view</returns>
        public ModelView GetStandardModelView()
        {
            // noch nicht richtig implementiert
            ModelView view = new ModelView(this);
            view.ProjectedModel = new ProjectedModel(GetActiveModel(), new Projection(Projection.StandardProjection.FromTop));
            view.Projection.DrawingPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            return view;
        }
        /// <summary>
        /// Creates a <see cref="ModelView"/> that shows the given model in one of the standard projections: 0 the view from
        /// top with the x/y plane as drawing plane, 1 the view from the right with the y/z plane, 2 the view from the front
        /// with the x/z plane, 3 an isometric view in the direction (-1, -1, -1) with the drawing plane perpendicular to it.
        /// Any other index gives the view from top. A new view is created on each call.
        /// </summary>
        /// <param name="m">The model to show</param>
        /// <param name="Index">Selects the projection, 0 to 3</param>
        /// <returns>The new model view</returns>
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
        /// Checks whether the project has been modified (<see cref="IsModified"/>) and, if so, asks the user whether to save
        /// it; on "yes" the project is written to its file. Without a user interface (setting "DontUse.WindowsForms") a
        /// modified project is saved without asking.
        /// </summary>
        /// <returns>false if the user pressed cancel, true otherwise (saved or discarded)</returns>
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
        /// <summary>
        /// Creates a project with one empty <see cref="Model"/> and the standard views of it (from top, from right, from
        /// front and an isometric view). The model's extent is taken from the global setting "DefaultModelSize", or is
        /// 0 to 100 in each direction. The project has the default attribute lists (layers, colours, styles...), so
        /// objects can be given default attributes with <see cref="SetDefaults"/>. This is the project to start with when
        /// CADability is used as a library without a user interface; <see cref="GetActiveModel"/> returns the model to add
        /// objects to. The imports create their project this way.
        /// </summary>
        /// <returns>the new project</returns>
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
        /// <summary>
        /// The <see cref="UndoRedoSystem"/> of this project, which records the undoable changes of the models and objects of
        /// the project; the models of the project share it (<see cref="Model.Undo"/>). Use
        /// <see cref="UndoRedoSystem.UndoFrame"/> to combine several changes into one undo step.
        /// </summary>
        public UndoRedoSystem Undo
        {
            get { return undoRedoSystem; }
        }
        
        /// <summary>
        /// Writes the project data to the given stream using binary serialization. Resets the IsModified flag.
        /// This method is obsolete. Use <see cref="WriteToJson"/> instead for new code.
        /// </summary>
        /// <param name="stream">Stream where to write the data</param>
        [Obsolete("Use WriteToJson instead. Binary serialization is deprecated in favor of JSON format.")]
        public void WriteToStream(Stream stream)
        {
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File, null));
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            formatter.Serialize(stream, this);
            isModified = false;
        }

        /// <summary>
        /// Writes the Project to a JSON formatted stream. Resets the IsModified flag.
        /// </summary>
        /// <param name="stream">The Stream to write the JSON serialized project data to</param>
        public void WriteToJson(Stream stream)
        {
            // Create a new JsonSerialize instance to handle serialization
            JsonSerialize js = new JsonSerialize();

            // Serialize this project instance to the stream in JSON format
            js.ToStream(stream, this);
            isModified = false;
        }

        /// <summary>
        /// Saves the project in a file with the given FileName, in JSON format.
        /// If FileName is null, a SaveFileDialog is presented.
        /// </summary>
        /// <param name="FileName">The file name</param>
        /// <returns>true, if successful, false if the user pressed escape in the SaveFileDialog</returns>
        public virtual bool WriteToFile(string FileName)
        {
            return WriteToFile(FileName, true);
        }
        public virtual bool WriteToFile(string FileName, bool useProgress)
        {
            // the json format is the only format we write, no more writing in binary ISerializable format
            if (FileName == null || FileName.Length == 0)
            {
                int filterIndex = 0;
                if (Frame.UIService.ShowSaveFileDlg("Project.WriteToFile", StringTable.GetString("MenuId.File.Save.As"), StringTable.GetString("File.CADability.Filter.json"), ref filterIndex, ref FileName) != Substitutes.DialogResult.OK)
                    return false;
            }
            if (FileName != null && FileName.Length > 0)
            {
                UIProgressBar progress = useProgress ? new UIProgressBar("saving cdb file") : null;
                try
                {
                    // JsonSerialize.ToStream does a step for every IGeoObject it writes
                    if (progress != null) progress.SetStepIncrement(100.0 / Math.Max(1, CountSerializedGeoObjects()));
                    fileName = FileName;
                    using (Stream stream = File.Open(FileName, FileMode.Create))
                    {
                        JsonSerialize js = new JsonSerialize();
                        js.ToStream(stream, this, progress, false);
                    }
                    isModified = false;
                }
                finally
                {
                    if (progress != null) progress.SetCompleted();
                }
            }
            return true;
        }

        /// <summary>
        /// Counts the <see cref="IGeoObject"/>s which <see cref="JsonSerialize.ToStream(Stream, object, IJsonProgress, bool)"/>
        /// writes for this project: the objects of all models and of the symbol list together with the objects they contain.
        /// Every object is counted once, as it is written once.
        /// </summary>
        internal int CountSerializedGeoObjects()
        {
            HashSet<IGeoObject> counted = new HashSet<IGeoObject>();
            for (int i = 0; i < GetModelCount(); ++i)
            {
                foreach (IGeoObject geo in GetModel(i)) CountGeoObjects(geo, counted);
            }
            foreach (IGeoObject geo in symbolList) CountGeoObjects(geo, counted);
            return counted.Count;
        }

        private static void CountGeoObjects(IGeoObject geo, HashSet<IGeoObject> counted)
        {
            if (geo == null || !counted.Add(geo)) return;
            switch (geo)
            {
                case Block block: // also Hatch
                    foreach (IGeoObject child in block.Children) CountGeoObjects(child, counted);
                    break;
                case BlockRef blockRef:
                    CountGeoObjects(blockRef.ReferencedBlock, counted);
                    break;
                case GeoObject.Path path:
                    foreach (ICurve curve in path.Curves) CountGeoObjects(curve as IGeoObject, counted);
                    break;
                case Solid solid:
                    foreach (Shell shell in solid.Shells) CountGeoObjects(shell, counted);
                    break;
                case Shell shell:
                    foreach (Face face in shell.Faces) CountGeoObjects(face, counted);
                    break;
                case Face face:
                    foreach (Edge edge in face.AllEdges) CountGeoObjects(edge.Curve3D as IGeoObject, counted);
                    break;
            }
        }

        /// <summary>
        /// Saves a copy of the project in the given file (JSON format, like <see cref="WriteToFile(string)"/>) from which all
        /// <see cref="CADability.UserData"/> of the project, of its models and of all their objects (including child objects)
        /// has been removed. This project is not changed. Useful to pass a project on without application specific data.
        /// </summary>
        /// <param name="fileName">The name of the file to write</param>
        /// <returns>true on success, false if the copy could not be made or written</returns>
        public bool WriteToFileWithoutUserData(string fileName)
        {
            try
            {
                // copy the project through JSON: the binary serialization is not available on .NET 5 and later
                MemoryStream ms = new MemoryStream();
                new JsonSerialize().ToStream(ms, this); // closes the stream
                Project pr = Project.ReadFromJson(new MemoryStream(ms.ToArray()));
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
        /// The DXF versions behind the choices of the setting "DxfDwg.Version", in the order of
        /// "DxfDwg.Version.Values" in the string tables: 2.5, 2.6, 9, 10, 11/12, 13, 14, 2000,
        /// 2004, 2007, 2010, 2013, 2018. ACadSharp cannot write the versions before AutoCAD 13,
        /// they are <see cref="ACadVersion.Unknown"/> here. New versions are appended to the end
        /// of the list, so that a stored selection keeps its meaning.
        /// </summary>
        private static readonly ACadVersion[] dxfDwgVersions = new ACadVersion[]
        {
            ACadVersion.Unknown, // 2.5
            ACadVersion.Unknown, // 2.6
            ACadVersion.Unknown, // 9
            ACadVersion.Unknown, // 10
            ACadVersion.Unknown, // 11/12
            ACadVersion.AC1012,  // 13
            ACadVersion.AC1014,  // 14
            ACadVersion.AC1015,  // 2000
            ACadVersion.AC1018,  // 2004
            ACadVersion.AC1021,  // 2007
            ACadVersion.AC1024,  // 2010
            ACadVersion.AC1027,  // 2013
            ACadVersion.AC1032,  // 2018
        };
        /// <summary>
        /// The DXF version <see cref="Export"/> writes, from the setting "DxfDwg.Version" (DXF/DWG
        /// Version in the export settings of the control center). Its default is AutoCAD 2000
        /// (AC1015), the widest readership. AutoCAD 2010 (AC1024) is the first version whose DXF
        /// knows the arc length dimension (ARC_DIMENSION). A choice ACadSharp cannot write falls
        /// back to AutoCAD 2000.
        /// </summary>
        private static ACadVersion DxfExportVersion()
        {
            int selection = Settings.GlobalSettings.GetIntValue("DxfDwg.Version", 7);
            if (selection >= 0 && selection < dxfDwgVersions.Length && dxfDwgVersions[selection] != ACadVersion.Unknown)
                return dxfDwgVersions[selection];
            Trace.WriteLine("dxf: setting DxfDwg.Version (selection " + selection.ToString() + ") is not a version that can be written, using AC1015");
            return ACadVersion.AC1015;
        }
        /// <summary>
        /// The DWG version <see cref="Export"/> writes: the version of <see cref="DxfExportVersion"/>, but at
        /// least AutoCAD 2004 (AC1018). ACadSharp writes no DWG before AutoCAD 14, and before AutoCAD 2004 it
        /// drops the polyface meshes, which are the export of faces, shells and solids. It cannot write
        /// AutoCAD 2007 (AC1021) DWG either, so that becomes AutoCAD 2010 (AC1024).
        /// </summary>
        private static ACadVersion DwgExportVersion()
        {
            ACadVersion version = DxfExportVersion();
            if (version < ACadVersion.AC1018) return ACadVersion.AC1018;
            if (version == ACadVersion.AC1021) return ACadVersion.AC1024;
            return version;
        }
        /// <summary>
        /// Exports the project in one of the following formats: cdb (CADability's own JSON format, same as
        /// <see cref="WriteToFile"/>), dxf, dwg, step (or stp) and stl. The case of <paramref name="format"/> does not
        /// matter, the extension of the file name is not used. DXF and DWG write all models (see <see cref="DXF.Export"/>;
        /// the DXF version is the setting "DxfDwg.Version", DWG is written as AutoCAD 2004 or later), STEP and STL write
        /// the active model only. STEP writes solids, shells and faces as boundary representation and curves as curve
        /// sets (see <see cref="ExportStep"/>), STL writes the triangulation of the faces with the precision of the setting
        /// "Export.STL.Precision" (see <see cref="ExportToSTL"/>).
        /// </summary>
        /// <param name="fileName">Path and filename for the generated output file</param>
        /// <param name="format">Format, one of the strings: cdb, dxf, dwg, step, stp, stl</param>
        /// <returns>true, if the file was written, false for a format that cannot be exported (e.g. dxb, iges, html)</returns>
        public bool Export(string fileName, string format)
        {
            format = format.ToLower();
            switch (format)
            {
                case "cdb": return WriteToFile(fileName);
                case "html":
                    break; // not supported, ExportToWebGl needs a template that is not part of CADability
                case "dxf":
                    CADability.DXF.Export export = new DXF.Export(DxfExportVersion());
                    export.WriteToFile(this, fileName);
                    return true;
                case "dwg":
                    CADability.DXF.Export dwgExport = new DXF.Export(DwgExportVersion());
                    dwgExport.WriteToFile(this, fileName, true);
                    return true;
                case "dxb":
                    break;
                case "igs":
                case "iges":
                    break;
                case "step":
                case "stp":
                    ExportStep exportStep = new ExportStep();
                    exportStep.WriteToFile(fileName, this);
                    return true;
                case "vrml":
                case "wrl":
                    break;
                case "stl":
                    ExportToSTL(fileName);
                    return true;
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
        /// <summary>
        /// Writes the active model to a binary STL file: the triangulation of all faces, shells and solids, with the
        /// precision (maximal distance between the triangles and the surface) of the setting "Export.STL.Precision",
        /// 0.005 by default. Curves, text and other objects without faces are not written.
        /// </summary>
        /// <param name="fileName">Path and filename of the STL file</param>
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
        /// <summary>
        /// Writes the active model (<see cref="GetActiveModel"/>) as a HTML file that displays it with WebGL, see
        /// <see cref="CADability.ExportToWebGl"/>. The HTML template "WebGLS.html" is expected in the directory of the
        /// CADability assembly. If there is an active view, its view direction is used as the initial view direction.
        /// </summary>
        /// <param name="fileName">The name of the HTML file to write</param>
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

        /// <summary>
        /// Reads the GDI+ objects in binary project files of CADability 1.x without GDI+ (issue #412): the bitmaps of pictures
        /// and icons as <see cref="LegacyImageData"/>, i.e. as their image file bytes, and the page settings with all their
        /// parts as <see cref="LegacyPrintingData"/>, from which a <see cref="PageSetup"/> is made. All other types are bound
        /// as usual.
        /// </summary>
        private class LegacySystemDrawingBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                return BindLegacySystemDrawing(assemblyName, typeName); // null: the formatter's own binding
            }
        }
        private static Type BindLegacySystemDrawing(string assemblyName, string typeName)
        {
            if (assemblyName == null || !assemblyName.StartsWith("System.Drawing")) return null;
            if (LegacyImageData.IsGdiImageType(typeName)) return typeof(LegacyImageData);
            if (LegacyPrintingData.IsPrintingType(typeName)) return typeof(LegacyPrintingData);
            return null;
        }
        private class CondorSerializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                System.Diagnostics.Trace.WriteLine("BindToType: " + assemblyName + ", " + typeName);
                Type legacy = BindLegacySystemDrawing(assemblyName, typeName);
                if (legacy != null) return legacy;
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
        /// Creates a new project reading the data from the given stream using binary deserialization.
        /// This method is obsolete but kept for backward compatibility to read legacy binary format files.
        /// Use <see cref="ReadFromJson(Stream)"/> for new code.
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>The deserialized Project or null if deserialization fails</returns>
        [Obsolete("Use ReadFromJson for new code. This method is kept only for reading legacy binary format files.")]
        public static Project ReadFromStream(Stream stream)
        {
            // BinaryFormatter formatter = new BinaryFormatter();
            FinishDeserialization finishDeserialization = new FinishDeserialization();
            BinaryFormatter formatter = new BinaryFormatter(); 
            
            //(null,new StreamingContext(StreamingContextStates.File,finishDeserialization));
            // so kann man ggf. die ganze Serialisierung auf JSON umstellen:
            // SerializationInfo si = new SerializationInfo(typeof(Project), new FormatterConverter());
            // damit kann man GetData aufrufen und dann si wiederum nach zugefügten Einträgen iterieren
            // mit NewtonSoft (https://github.com/JamesNK/Newtonsoft.Json) scheint das mehr Sinn zu machen als mit DataContract

#if FW2
            formatter.Binder = new CondorSerializationBinder(); // das soll hier gleich laufen, damit nicht versucht wird die CADability.dll zu laden
#else
            formatter.Binder = new LegacySystemDrawingBinder();
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
                    if (ex.Message.Contains("CompareInfo")) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is ArgumentNullException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is ArgumentOutOfRangeException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is OverflowException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is SerializationException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is OutOfMemoryException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);
                    if (ex is System.IO.FileLoadException) throw new ProjectOldVersionException("probably old version cdb file, must be converted", ex);

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
        /// Reads a project from a file in CADability's own format (cdb): the JSON format written by
        /// <see cref="WriteToFile"/>, or the binary format of older versions, which is converted when read. To read
        /// DXF, DWG, STEP or STL files use <see cref="ReadFromFile(string, string)"/>.
        /// </summary>
        /// <param name="FileName">The name of the file</param>
        /// <returns>The project read from the file, or null when <paramref name="FileName"/> is null or the file does not
        /// contain a project</returns>
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
            UIProgressBar progress = useProgress ? new UIProgressBar("reading cdb file") : null;
            try
            {
                return ReadCdbFile(FileName, useProgress, progress);
            }
            finally
            {
                if (progress != null) progress.SetCompleted();
            }
        }

        private static Project ReadCdbFile(string FileName, bool useProgress, IJsonProgress progress)
        {
            Project res = null;
            using (FileStream stream = File.Open(FileName, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
            {
                int firstByte = stream.ReadByte();
                stream.Seek(0, SeekOrigin.Begin);
                if (firstByte == 123)
                {
                    res = ReadFromJson(stream, progress);
                }
                else
                {
                    try
                    {
#pragma warning disable CS0618 // reading the legacy binary format is what ReadFromStream is kept for
                        res = ReadFromStream(stream);
#pragma warning restore CS0618
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
                                    res = ReadConvertedFile(FileName, useProgress);
                                }
                            }
                        }
                    }
                }
                if (res != null)
                {
                    res.fileName = FileName;
                }
                return res;
            }
        }

        /// <summary>
        /// Reads a Project from a JSON formatted stream.
        /// </summary>
        /// <param name="stream">The Stream containing JSON serialized project data</param>
        /// <returns>The deserialized Project or null if deserialization fails</returns>
        public static Project ReadFromJson(Stream stream)
        {
            return ReadFromJson(stream, null);
        }

        /// <summary>
        /// Reads a Project from a JSON formatted stream like <see cref="ReadFromJson(Stream)"/> and reports the progress of
        /// reading to <paramref name="progress"/>.
        /// </summary>
        /// <param name="stream">The Stream containing JSON serialized project data</param>
        /// <param name="progress">receives the progress, may be null</param>
        /// <returns>The deserialized Project or null if deserialization fails</returns>
        public static Project ReadFromJson(Stream stream, IJsonProgress progress)
        {
            // Create a new JsonSerialize instance to handle deserialization
            JsonSerialize js = new JsonSerialize();

            // Attempt to deserialize the stream into a Project object
            if (!(js.FromStream(stream, progress) is Project res))
                return null;

            // Update all attribute lists to ensure they are synchronized with the project's attributes
            AttributeListContainer.UpdateLists(res, true);

            // Reset the modified flag since we just loaded the project from file
            res.isModified = false;
            return res;
        }

        private static Project ReadConvertedFile(string FileName, bool useProgress)
        {
            Project res = null;
            FileStream stream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
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
        private static Project ImportDXF(string filename, bool useProgress)
        {
            CADability.DXF.Import import = new DXF.Import(filename, useProgress ? new UIProgressBar("import dxf") : null);
            return import.Project;
        }

        private static Project ImportDWG(string filename, bool useProgress)
        {
            try
            {
                CADability.DXF.Import import = new DXF.Import(filename, useProgress ? new UIProgressBar("import dwg") : null);
                return import.Project;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to read DWG file '" + filename + "': " + ex.Message, ex);
            }
        }
        /// <summary>
        /// Reads a file of the given format and returns it as a new project. <paramref name="Format"/> is one of
        /// "cdb" (CADability's own format, see <see cref="ReadFromFile(string)"/>), "dxf", "dwg", "step" or "stp" and "stl";
        /// the case does not matter, the extension of the file name is not used. For the exchange formats the project is
        /// created with <see cref="CreateSimpleProject"/> and the imported objects are added to its model: DXF and DWG
        /// bring their layers, blocks, colours, line styles and hatch styles along (see <see cref="DXF.Import"/>), STEP files
        /// give solids, open shells and faces, named like their products and arranged in <see cref="Block"/>s
        /// according to the assembly structure when the setting "ImportStep.UseHierarchy" is true (see
        /// <see cref="ImportStep"/>), and STL files give a solid for every closed set of triangles and a shell for every
        /// open one. Objects without a colour or layer get the project's standard ones.
        /// A DWG file that cannot be read throws an <see cref="InvalidOperationException"/>; a DXF or STEP file that cannot
        /// be read throws an exception as well.
        /// </summary>
        /// <param name="FileName">The name of the file</param>
        /// <param name="Format">The format: cdb, dxf, dwg, step, stp or stl</param>
        /// <param name="useProgress">no longer used, the progress bar is shown by the frame when there is one</param>
        /// <param name="makeCompounds">no longer used</param>
        /// <returns>The new project, or null for an unknown or unsupported format (e.g. brep, sat, x_t) and for a STL
        /// file without shells</returns>
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
                case "dxf": return ImportDXF(FileName, useProgress);
                case "dwg": return ImportDWG(FileName, useProgress);
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
        /// <summary>
        /// Reads a file of the given format and returns it as a new project, see
        /// <see cref="ReadFromFile(string, string, bool, bool)"/>.
        /// </summary>
        /// <param name="FileName">The name of the file</param>
        /// <param name="Format">The format: cdb, dxf, dwg, step, stp or stl</param>
        /// <returns>The new project, or null for an unknown or unsupported format</returns>
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
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <inheritdoc/>
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
        /// <summary>
        /// Delegate for <see cref="RefreshEvent"/>.
        /// </summary>
        /// <param name="sender">The project</param>
        /// <param name="args">Not used, null</param>
        public delegate void RefreshDelegate(object sender, EventArgs args);
        /// <summary>
        /// Raised when the display of the project's objects must be refreshed because an attribute (a layer, colour, line
        /// pattern, hatch style...) of the attribute lists of the project has been modified, and when
        /// <see cref="DeferRefresh"/> is reset to false. The frame listens to it.
        /// </summary>
        public event RefreshDelegate RefreshEvent;
        /// <summary>
        /// Delegate for <see cref="ViewChangedEvent"/>.
        /// </summary>
        /// <param name="sender">The project</param>
        /// <param name="viewWhichChanged">The view concerned; currently always null, meaning that the list of views has changed</param>
        public delegate void ViewChangedDelegate(Project sender, IView viewWhichChanged);
        /// <summary>
        /// Raised when the list of views of the project has changed, i.e. a model view or a layout has been added or removed
        /// (e.g. by <see cref="RemoveModelView(ModelView)"/> or by the commands of the project in the control center). The
        /// frame listens to it to update its views.
        /// </summary>
        public event ViewChangedDelegate ViewChangedEvent;
        /// <summary>
        /// Delegate for <see cref="ModelsChangedEvent"/>.
        /// </summary>
        /// <param name="sender">The project</param>
        /// <param name="model">The model that was added, removed or renamed</param>
        /// <param name="added">true if the model was added, false if it was removed or renamed</param>
        public delegate void ModelsChangedDelegate(Project sender, Model model, bool added);
        /// <summary>
        /// Raised after a model has been added to the project (<see cref="AddModel(Model)"/>), removed from it
        /// (<see cref="RemoveModel(Model)"/>) or renamed. The views listen to it to update their list of models.
        /// </summary>
        public event ModelsChangedDelegate ModelsChangedEvent;
        /// <summary>
        /// Delegate for <see cref="ProjectModifiedEvent"/>.
        /// </summary>
        /// <param name="sender">The project</param>
        public delegate void ProjectModifiedDelegate(Project sender);
        /// <summary>
        /// Raised whenever <see cref="IsModified"/> is set to true, i.e. after a change that has to be saved. It is not raised
        /// when the flag is reset by saving.
        /// </summary>
        public event ProjectModifiedDelegate ProjectModifiedEvent;
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


        /// <summary>
        /// The colours (<see cref="CADability.Attribute.ColorList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public ColorList ColorList
        {
            get
            {
                return attributeLists["ColorList"] as ColorList;
            }
        }
        /// <summary>
        /// The layers (<see cref="CADability.Attribute.LayerList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public LayerList LayerList
        {
            get
            {
                return attributeLists["LayerList"] as LayerList;
            }
        }
        /// <summary>
        /// The hatch styles (<see cref="CADability.Attribute.HatchStyleList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public HatchStyleList HatchStyleList
        {
            get
            {
                return attributeLists["HatchStyleList"] as HatchStyleList;
            }
        }
        /// <summary>
        /// The dimension styles (<see cref="CADability.Attribute.DimensionStyleList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public DimensionStyleList DimensionStyleList
        {
            get
            {
                return attributeLists["DimensionStyleList"] as DimensionStyleList;
            }
        }
        /// <summary>
        /// The line widths (<see cref="CADability.Attribute.LineWidthList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public LineWidthList LineWidthList
        {
            get
            {
                return attributeLists["LineWidthList"] as LineWidthList;
            }
        }
        /// <summary>
        /// The line patterns (<see cref="CADability.Attribute.LinePatternList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
        public LinePatternList LinePatternList
        {
            get
            {
                return attributeLists["LinePatternList"] as LinePatternList;
            }
        }
        /// <summary>
        /// The styles (named combinations of attributes) (<see cref="CADability.Attribute.StyleList"/>) of this project. The attribute lists of a project contain the
        /// attributes the objects of its models use; they are shown in the project tab of the control center, saved with the
        /// project and initialized from the global settings when the project is created. New objects get the current entry
        /// (see <see cref="SetDefaults(IGeoObject)"/>).
        /// </summary>
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
        /// <summary>
        /// While true, modifications of attributes do not raise the <see cref="RefreshEvent"/>; setting it back to false
        /// raises the event once. Use it to bracket a series of attribute changes so that the display is refreshed only once.
        /// </summary>
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
        /// <summary>
        /// Gives the object the default attributes of this project: the default <see cref="Style"/> for its kind of object
        /// (<see cref="IGeoObject.PreferredStyle"/>), and the current layer, colour, dimension style, line width, line pattern
        /// and hatch style of the project's attribute lists for every attribute the object does not have yet. The
        /// construct actions and the methods of <see cref="Make3D"/> that take a project call this. Call it for objects you
        /// create yourself before adding them to a model, otherwise they have no layer and colour and are drawn in the
        /// default colour.
        /// </summary>
        /// <param name="go">the object to set the attributes of</param>
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
        /// <summary>
        /// Writes the given object to a file with the <see cref="BinaryFormatter"/>, overwriting an existing file. Obsolete:
        /// the BinaryFormatter is deprecated and disabled on current .NET versions, and CADability writes its own files in
        /// JSON format (<see cref="JsonSerialize"/>). Not used by CADability itself.
        /// </summary>
        /// <param name="toSerialize">The object to write</param>
        /// <param name="fileName">The name of the file</param>
        public static void SerializeObject(ISerializable toSerialize, string fileName)
        {
            Stream stream = File.Open(fileName, System.IO.FileMode.Create);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, toSerialize);
            stream.Close();
            stream.Dispose();
        }
        /// <summary>
        /// Reads an object written by <see cref="SerializeObject(ISerializable, string)"/> from the file. Obsolete, see there.
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <returns>The object read</returns>
        public static object DeserializeObject(string fileName)
        {
            Stream stream = File.Open(fileName, System.IO.FileMode.Open, FileAccess.Read, FileShare.Read);
            BinaryFormatter formatter = new BinaryFormatter();
            object res = formatter.Deserialize(stream);
            stream.Close();
            stream.Dispose();
            return res;
        }
#if DEBUG
        /// <summary>
        /// Serializes the given object into a memory stream and immediately deserializes it again. This can be used to test for
        /// problems with the serialization (e.g. loss of data). The returned object has no connection to any other object any
        /// more; it is a deep copy of the input.
        /// </summary>
        /// <param name="toSerialize">The object to serialize; it must be serializable</param>
        /// <returns>The deserialized copy</returns>
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
            try
            {
                ser_gdiViews = info.GetValue("GdiViews", typeof(GDI2DView[])) as GDI2DView[];
            }
            catch (SerializationException)
            {
            }
            try
            {
                activeViewName = info.GetString("ActiveViewName");
            }
            catch (SerializationException)
            {
            }
            if (info.MemberCount > 12) // to avoid exceptions
            {
                foreach (SerializationEntry entry in info)
                {
                    if (entry.Name == "DefaultPageSetup") defaultPageSetup = entry.Value as PageSetup;
                    else if (entry.Name == "DefaultPageSettings" && defaultPageSetup == null) defaultPageSetup = PageSetup.FromSerialized(entry.Value); // 1.x
                }
            }
            base.resourceIdInternal = "ProjectSettings";
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Models", models);
            info.AddValue("ActiveModelIndex", activeModelIndex);
            info.AddValue("AttributeLists", attributeLists);
            info.AddValue("UserData", UserData);
            info.AddValue("SymbolList", symbolList);
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                info.AddValue("Layouts", layouts.ToArray()); // Generics erwarten die exakte Version beim Deserialisieren, geht also nicht
                info.AddValue("GdiViews", gdiViews.ToArray());
            }
            info.AddValue("ProjectedModels", projectedModels.ToArray());
            info.AddValue("NamedValues", namedValues);
            info.AddValue("FilterList", filterList);
            info.AddValue("AnimatedViews", animatedViews.ToArray());
            info.AddValue("ActiveViewName", activeViewName);
            // the neutral page settings, not the GDI+ PageSettings, which cannot be serialized on .NET Core (issue #412)
            PageSetup pageSetup = DefaultPageSetup;
            if (pageSetup != null) info.AddValue("DefaultPageSetup", pageSetup);
        }
        /// <summary>
        /// Implements <see cref="IJsonSerialize.GetObjectData(IJsonWriteData)"/>: writes the models, the active model, the
        /// attribute lists, the user data, the symbol list, the projected models, the named values, the filter list, the
        /// animated views and the name of the active view, and, unless the global setting "DontSave.System.Drawing" is set,
        /// the layouts, the GDI views and the page settings. This is the content of CADability's project files.
        /// </summary>
        /// <param name="data">Receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Models", models);
            data.AddProperty("ActiveModelIndex", activeModelIndex);
            data.AddProperty("AttributeLists", attributeLists);
            data.AddProperty("UserData", UserData);
            data.AddProperty("SymbolList", symbolList);
            if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
            {
                data.AddProperty("Layouts", layouts);
                data.AddProperty("GdiViews", gdiViews);
            }
            data.AddProperty("ProjectedModels", projectedModels);
            data.AddProperty("NamedValues", namedValues);
            data.AddProperty("FilterList", filterList);
            data.AddProperty("AnimatedViews", animatedViews);
            data.AddProperty("ActiveViewName", activeViewName);
            // the neutral page settings, not the GDI+ PageSettings, which cannot be serialized on .NET Core (issue #412)
            PageSetup pageSetup = DefaultPageSetup;
            if (pageSetup != null) data.AddProperty("DefaultPageSetup", pageSetup);
        }

        /// <summary>
        /// Implements <see cref="IJsonSerialize.SetObjectData(IJsonReadData)"/>: reads the data written by
        /// <see cref="GetObjectData(IJsonWriteData)"/>. Properties missing in the file get their default values; the printer
        /// settings are skipped on platforms without System.Drawing printing. The object is completed (owner of the attribute
        /// lists, views, event subscriptions) in the callback <see cref="IJsonSerializeDone.SerializationDone(JsonSerialize)"/>.
        /// </summary>
        /// <param name="data">The data to read from</param>
        public void SetObjectData(IJsonReadData data)
        {
            int version = data.Version;
            models = new ArrayList(data.GetPropertyOrDefault<List<Model>>("Models"));
            activeModelIndex = data.GetPropertyOrDefault<int>("ActiveModelIndex");
            attributeLists = data.GetPropertyOrDefault<Hashtable>("AttributeLists");
            UserData = data.GetPropertyOrDefault<UserData>("UserData");
            symbolList = data.GetPropertyOrDefault<GeoObjectList>("SymbolList");
            try
            {
                if (!Settings.GlobalSettings.GetBoolValue("DontSave.System.Drawing", false))
                {
                    layouts = data.GetPropertyOrDefault<List<Layout>>("Layouts");
                    gdiViews = data.GetPropertyOrDefault<List<GDI2DView>>("GdiViews");
                }
            }
            catch (Exception e) when (IsSystemDrawingUnavailable(e)) { }
            projectedModels = data.GetPropertyOrDefault<List<ProjectedModel>>("ProjectedModels");
            namedValues = data.GetPropertyOrDefault<NamedValuesProperty>("NamedValues");
            filterList = data.GetPropertyOrDefault<FilterList>("FilterList");
            animatedViews = data.GetPropertyOrDefault<List<AnimatedView>>("AnimatedViews");
            activeViewName = data.GetPropertyOrDefault<string>("ActiveViewName");
            defaultPageSetup = data.GetPropertyOrDefault<PageSetup>("DefaultPageSetup");
            if (defaultPageSetup == null && data.HasProperty("DefaultPageSettings")) defaultPageSetup = PageSetup.FromSerialized(data.GetProperty<object>("DefaultPageSettings")); // 1.x
            printDocument = null;
            data.RegisterForSerializationDoneCallback(this);
        }
        /// <summary>
        /// System.Drawing printing is only supported on Windows. Elsewhere the first use of a PrintDocument does not
        /// throw the PlatformNotSupportedException itself but a TypeInitializationException of a printing class that
        /// wraps it, so both mean that the printer settings cannot be restored and the project is read without them.
        /// </summary>
        private static bool IsSystemDrawingUnavailable(Exception e)
        {
            return e is PlatformNotSupportedException || (e is TypeInitializationException && e.GetBaseException() is PlatformNotSupportedException);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (IAttributeList list in attributeLists.Values)
            {
                // a list may complete itself in its own callback (the LayerList sorts its layers by name there),
                // which has to happen before the lists are updated below
                jsonSerialize.InvokeSerializationDoneCallback(list);
                list.Owner = this;
            }
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
            if (layouts == null) layouts = new List<Layout>();
            foreach (Layout l in layouts)
            {
                l.project = this;
            }
        }
        #endregion
        #region ICommandHandler Members
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
                case "MenuId.View.Layout.NewLayout":
                    OnNewLayout();
                    return true;
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
        /// <summary>
        /// Not implemented, always throws a <see cref="NotImplementedException"/>. It exists only beside the
        /// <see cref="IEnumerable"/> implementation, which enumerates the models; add models with <see cref="AddModel(Model)"/>.
        /// </summary>
        /// <param name="toAdd">Not used</param>
        /// <exception cref="NotImplementedException">always</exception>
        public void Add(object toAdd)
        {
            throw new NotImplementedException();
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
            if (ser_gdiViews != null)
            {
                gdiViews = new List<GDI2DView>(ser_gdiViews);
                ser_gdiViews = null;
            }
            else
            {
                gdiViews = new List<GDI2DView>();
            }
        }

        #endregion

        /// <summary>
        /// It shows the progress bar during reading/writing a drawing file.
        /// </summary>
        private class UIProgressBar : DXF.IDxfProgress, IJsonProgress
        {
            string _text = null; //Text in the progress bar.
            double _increment = 1; //Increment value of the progress bar for each executed step. When all steps are completed it is 100.
            double _offset = 0; //Progress bar offset value >=0 <100. Used if reading/writing is done in more than one phase.
            int _step = 0; //Current step value.
            double _currentPercentualPosition = 0; //Current position of the progress bar.


            public UIProgressBar(string text)
            {
                _text = text;
            }

            public void SetStepIncrement(double increment)
            {
                _step = 0;
                _increment = increment;
                _offset = _currentPercentualPosition;
            }

            public void DoStep(int doneSteps = 1)
            {
                _step += doneSteps;
                double percentual = Math.Min(100, Math.Round(_offset + _increment * _step, 1));
                if (percentual > _currentPercentualPosition) //Without this import will be slower.
                {
                    FrameImpl.MainFrame?.UIService.ShowProgressBar(true, percentual, _text);
                    //Debug.WriteLine($"ProgressStatus={percentual}");
                }
                _currentPercentualPosition = percentual;
            }

            public void SetCompleted()
            {
                FrameImpl.MainFrame?.UIService.ShowProgressBar(false);
                //Debug.WriteLine($"ProgressStatus=completed");
            }
        }
    }
}
