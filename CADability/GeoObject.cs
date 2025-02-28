using CADability.UserInterface;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;


/// <summary>
/// Namespace GeoObject
/// </summary>
namespace CADability.GeoObject
{
    using CADability.Actions;
    using CADability.Attribute;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// This class is used as a parameter in the <see cref="ChangeDelegate"/> event of IGeoObject 
    /// (see <see cref="IGeoObject.WillChangeEvent"/>).
    /// </summary>

    public class GeoObjectChange : ReversibleChange
    {
        /// <summary>
        /// Notifies that only an attribute was changed in contrast to a change of the geometry.
        /// </summary>
        public bool OnlyAttributeChanged;
        /// <summary>
        /// Notifies that this change doesn't require an undo operation
        /// </summary>
        public bool NoUndoNecessary;
        /// <summary>
        /// Creates a new GeoObjectChange object. See the appropriate constructor of <see cref="ReversibleChange"/> for details.
        /// </summary>
        /// <param name="objectToChange">The object which will be or was changed</param>
        /// <param name="interfaceForMethod">the interface on which contains the method or property</param>
        /// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
        /// <param name="parameters">The parameters neede to call this method or property</param>
        public GeoObjectChange(IGeoObject objectToChange, Type interfaceForMethod, string methodOrPropertyName, params object[] parameters)
            : base(objectToChange, interfaceForMethod, methodOrPropertyName, parameters)
        {
            OnlyAttributeChanged = false;
            NoUndoNecessary = false;
        }
        /// <summary>
        /// Creates a new GeoObjectChange object. See the appropriate constructor of <see cref="ReversibleChange"/> for details.
        /// </summary>
        /// <param name="objectToChange">The object which will be or was changed</param>
        /// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
        /// <param name="parameters">The parameters neede to call this method or property</param>
        public GeoObjectChange(IGeoObject objectToChange, string methodOrPropertyName, params object[] parameters)
            : base(objectToChange, methodOrPropertyName, parameters)
        {
            OnlyAttributeChanged = false;
            NoUndoNecessary = false;
        }
        /// <summary>
        /// Creates a new GeoObjectChange object that reflects a modification by the <see cref="ModOp"/> m.
        /// </summary>
        /// <param name="objectToChange">The object which will be or was changed</param>
        /// <param name="m">the ModOp that changes or changed the object</param>
        public GeoObjectChange(IGeoObject objectToChange, ModOp m)
            : base(objectToChange, "ModifyInverse", m)
        {
            OnlyAttributeChanged = false;
            NoUndoNecessary = false;
        }
        // Wenn das gebraucht wird, dann mit anderen Parametern versehen sonst droht verwechslung mit obiger Methode (methodOrPropertyName)
        //public GeoObjectChange(IGeoObject objectToChange, string key, object oldValue)
        //    : base(objectToChange.UserData, "Add", key, oldValue)
        //{
        //    OnlyAttributeChanged = true;
        //    NoUndoNecessary = false;
        //}
    }

    /// <summary>
    /// Delegate method that is invoked when a GeoObject is about to change or did change.
    /// </summary>
    /// <param name="Sender">The GeoObject</param>
    /// <param name="Change">Type of change</param>
    public delegate void ChangeDelegate(IGeoObject Sender, GeoObjectChange Change);


    public interface ICategorizedDislayLists
    {
        void Add(Layer layer, bool addToFace, bool addToLinear, IGeoObject go);
    }
    internal struct DelayedDisplayObject
    {
        public ModOp modOp;
        public IGeoObject geoObject;
        public DelayedDisplayObject(ModOp modOp, IGeoObject geoObject)
        {
            this.modOp = modOp;
            this.geoObject = geoObject;
        }
    }
    internal class LayerToDisplayListDictionary: Dictionary<Layer, IPaintTo3DList>
    {
        public new void Clear()
        {
            foreach (KeyValuePair<Layer, IPaintTo3DList> item in this)
            {
                if (item.Value != null) item.Value.Dispose();
            }
            base.Clear();
        }
    }
    internal class CategorizedDislayLists : ICategorizedDislayLists
    {
        internal LayerToDisplayListDictionary layerFaceDisplayList; // Alle Faces, geordnet nach Layern
        internal LayerToDisplayListDictionary layerCurveDisplayList; // alle Curves etc. geordnet nach Layern
        internal LayerToDisplayListDictionary layerTransparentDisplayList; // Alle transparenten, geordnet nach Layern
        internal Dictionary<Layer, GeoObjectList> layerUnscaledObjects; // die Liste aller UnscaledObjects
        internal Dictionary<Layer, List<IGeoObject>> layerFaceObjects; // temporär Alle Faces, geordnet nach Layern
        internal Dictionary<Layer, List<IGeoObject>> layerCurveObjects; // temporär alle Curves etc. geordnet nach Layern
        internal Dictionary<Layer, List<IGeoObject>> layerTransparentObjects; // temporär Alle transparenten, geordnet nach Layern
        public Layer NullLayer;
        public CategorizedDislayLists()
        {
            layerFaceDisplayList = new LayerToDisplayListDictionary();
            layerCurveDisplayList = new LayerToDisplayListDictionary();
            layerTransparentDisplayList = new LayerToDisplayListDictionary();
            layerUnscaledObjects = new Dictionary<Layer, GeoObjectList>();
            layerFaceObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerCurveObjects = new Dictionary<Layer, List<IGeoObject>>();
            layerTransparentObjects = new Dictionary<Layer, List<IGeoObject>>();
            NullLayer = new Layer("NullLayer");
        }

        public void Finish(IPaintTo3D paintTo3D)
        {
            layerFaceDisplayList.Clear();
            layerCurveDisplayList.Clear();
            layerTransparentDisplayList.Clear();
            paintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly);
            foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerFaceObjects)
            {
                paintTo3D.OpenList("layerFaceObjects");
                foreach (IGeoObject go in kv.Value)
                {
                    go.PaintTo3D(paintTo3D);
                }
                layerFaceDisplayList[kv.Key] = paintTo3D.CloseList();
            }
            foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerTransparentObjects)
            {
                paintTo3D.OpenList("layerTransparentObjects");
                foreach (IGeoObject go in kv.Value)
                {
                    go.PaintTo3D(paintTo3D);
                }
                layerTransparentDisplayList[kv.Key] = paintTo3D.CloseList();
            }
            paintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
            foreach (KeyValuePair<Layer, List<IGeoObject>> kv in layerCurveObjects)
            {
                paintTo3D.OpenList("layerCurveObjects");
                foreach (IGeoObject go in kv.Value)
                {
                    go.PaintTo3D(paintTo3D);
                }
                layerCurveDisplayList[kv.Key] = paintTo3D.CloseList();
            }
            layerFaceObjects.Clear();
            layerCurveObjects.Clear();
            layerTransparentObjects.Clear();
        }
        #region ICategorizedDislayLists Members
        void ICategorizedDislayLists.Add(Layer layer, bool addToFace, bool addToLinear, IGeoObject go)
        {
            if (layer == null) layer = NullLayer;
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
            }
        }
        #endregion
    }

    /// <summary>
    /// An <see cref="IGeoObject"/> object is either owned by a <see cref="Model"/>
    /// or by a <see cref="Block"/> or it has no owner. The property <see cref="IGeoObject.Owner"/>
    /// sets or gets that owner. The <see cref="Model"/> or <see cref="Block"/> implement this
    /// interface. If you need more functionality than Add and Remove, try to cast
    /// IGeoObjectOwner to Model or Block.
    /// </summary>

    public interface IGeoObjectOwner
    {
        /// <summary>
        /// Removes the given <see cref="IGeoObject"/> from this container.
        /// </summary>
        /// <param name="toRemove">IGeoObject to remove</param>
        void Remove(IGeoObject toRemove);
        /// <summary>
        /// Adds the given <see cref="IGeoObject"/> to this container.
        /// </summary>
        /// <param name="toAdd"></param>
        void Add(IGeoObject toAdd);
    }

    public enum AttributeUsage
    {
        ColorByBlock = 0x01, LayerByBlock = 0x02, LinePatternByBlock = 0x04,
        LineWidthByBlock = 0x08
    }

    public enum ExtentPrecision { Raw, Exact }


    public interface IQuadTreeInsertableZ : IQuadTreeInsertable
    {
        double GetZPosition(GeoPoint2D p);
    }

    /// <summary>
    /// IGeoObject is the interface that all geometric entities must support 
    /// (see <a href="c90ccd0b-e1de-4859-8d4d-20e516a766cd.htm">GeoObject</a>). 
    /// </summary>

    public interface IGeoObject : ILayer, IStyle, IOctTreeInsertable, IComparable
    {
        /// <summary>
        /// Modifies this GeoObject with the given <see cref="ModOp">modification operation</see> 
        /// (includes moving, rotating, reflecting, scaling etc.).
        /// </summary>
        /// <param name="m">Operation to be applied</param>
        void Modify(ModOp m);
        /// <summary>
        /// Clones this GeoObject.
        /// </summary>
        /// <returns>the cloned GeoObject</returns>
        IGeoObject Clone();
        /// <summary>
        /// Determins whether this GeoObject has child objects. E.g. <see cref="Block"/> objects have children.
        /// This is necessary when the child objects have different Layers
        /// </summary>
        /// <returns></returns>
        bool HasChildren();
        /// <summary>
        /// Returns the number of children the GeoObject has. Simple GeoObjects (like <see cref="Line"/>) 
        /// dont have children.
        /// </summary>
        int NumChildren { get; }
        /// <summary>
        /// Returns the child with the given index. See <see cref="HasChildren"/>.
        /// </summary>
        /// <param name="Index">index of required child</param>
        /// <returns>this child</returns>
        IGeoObject Child(int Index);
        /// <summary>
        /// Returns the owner of this GeoObject. Each GeoObject has only one owner. 
        /// This might be a <see cref="Model"/> or a GeoObject derived from <see cref="Block"/>.
        /// </summary>
        IGeoObjectOwner Owner { get; set; }
        /// <summary>
        /// Copies the geometrical aspects of the given GeoObject to this GeoObject. Both objects
        /// must be of the same type. The attributes are not copied.
        /// </summary>
        /// <param name="ToCopyFrom">GeoObject to copy from</param>
        void CopyGeometry(IGeoObject ToCopyFrom);
        /// <summary>
        /// Copies the attributes from the given GeoObject to this GeoObject. The geometry remains unchanged.
        /// </summary>
        /// <param name="ToCopyFrom">GeoObject to copy from</param>
        void CopyAttributes(IGeoObject ToCopyFrom);
        /// <summary>
        /// Event that must be provided by each GeoObject. This event will be fired when
        /// the GeoObject is about to change (either geometrical aspects or attributes).
        /// </summary>
        event ChangeDelegate WillChangeEvent;
        /// <summary>
        /// Event that must be provided by each GeoObject. This event will be fired when
        /// the GeoObject did change (either geometrical aspects or attributes).
        /// </summary>
        event ChangeDelegate DidChangeEvent;
        /// <summary>
        /// Gets a <see cref="IShowProperty"/> object that represents the properties of
        /// this geoobject. The result will be used to display the properties in the 
        /// control center.
        /// </summary>
        /// <param name="Frame">IFrame reference used to check settings</param>
        /// <returns>properties</returns>
        IPropertyEntry GetShowProperties(IFrame Frame);
        /// <summary>
        /// Gets a list of <see cref="IShowProperty"/> objects that represent the non geometric
        /// properties of this geoobject. This list will be used during construct actions (e.g.
        /// when interactively drawing that object) to display properties like color or layer.
        /// </summary>
        /// <param name="Frame">IFrame reference used to check settings</param>
        /// <returns>list of properties</returns>
        IPropertyEntry[] GetAttributeProperties(IFrame Frame);
        /// <summary>
        /// Returns a bounding cube of the object. The object must fir into this cube. 
        /// There may be a smaller cube that contains the object if it is to expensive to
        /// calculate the exact cube.
        /// </summary>
        /// <returns>the bounding cube</returns>
        BoundingCube GetBoundingCube();
        /// <summary>
        /// When an IGeoObject changes the context that contains the lists of attributes
        /// it must replace its attributes by those given in the new context. If there are
        /// no appropriate attributes in the new context, these attributes must be included
        /// int this new context. This happens when drag and drop operations are executed
        /// or when a model is copied from one project to another project.
        /// <see cref="IGeoObjectImpl"/> contains a complete implementation regarding
        /// the attributes <see cref="Style"/>, <see cref="Layer"/>, <see cref="ColorDef"/>,
        /// <see cref="LineWidth"/>, <see cref="LinePattern"/>, <see cref="HatchStyle"/> and <see cref="DimensionStyle"/>
        /// </summary>
        /// <param name="alc">The context, usually a Project which contains the attribute lists</param>
        void UpdateAttributes(IAttributeListContainer alc);
        /// <summary>
        /// Gets the user data collection for this GeoObject. This is the way to connect your own
        /// objects with a GeoObject. See <see cref="UserData"/>.
        /// </summary>
        UserData UserData { get; }
        /// <summary>
        /// Returns true if this object uses the attribute in the parameter.
        /// Attributes may be CADability objects like <see cref="Layer"/> etc.
        /// or any user defined objects.
        /// </summary>
        /// <param name="Attribute">attribut to check</param>
        /// <returns>true if used by this GeoObject</returns>
        bool IsAttributeUsed(object Attribute);
        /// <summary>
        /// Sets an attribut to the GeoObject. There are the following types (keys) of attributes 
        /// predefined in CADability, which can be set with this method:
        /// <list type="">
        /// <item> "Layer": sets a <see cref="Layer"/></item>
        /// <item> "ColorDef": sets a <see cref="ColorDef"/></item>
        /// <item> "LineWidth": sets a <see cref="LineWidth"/></item>
        /// <item> "LinePattern": sets a <see cref="LinePattern"/></item>
        /// <item> "HatchStyle": sets a <see cref="HatchStyle"/></item>
        /// <item> "DimensionStyle": sets a <see cref="DimensionStyle"/></item>
        /// <item> "Style": sets a <see cref="Style"/></item>
        /// </list>
        /// Other attributes may be provided by the user.
        /// </summary>
        /// <param name="key">key or typename of attribute to set</param>
        /// <param name="toSet">attribute</param>
        void SetNamedAttribute(string key, INamedAttribute toSet);
        /// <summary>
        /// Gets an attribut from the GeoObject. There are the following types (keys) of attributes 
        /// predefined in CADability, which can be set with this method:
        /// <list type="">
        /// <item> "Layer": sets a <see cref="Layer"/></item>
        /// <item> "ColorDef": sets a <see cref="ColorDef"/></item>
        /// <item> "LineWidth": sets a <see cref="LineWidth"/></item>
        /// <item> "LinePattern": sets a <see cref="LinePattern"/></item>
        /// <item> "HatchStyle": sets a <see cref="HatchStyle"/></item>
        /// <item> "DimensionStyle": sets a <see cref="DimensionStyle"/></item>
        /// <item> "Style": sets a <see cref="Style"/></item>
        /// </list>
        /// Other attributes may be provided by the user.
        /// </summary>
        /// <param name="key">key or typename of the required attribute</param>
        /// <returns>the attribute or null, if there is no such attribute</returns>
        INamedAttribute GetNamedAttribute(string key);
        /// <summary>
        /// Gets all attributes that this GeoObject posesses.
        /// </summary>
        INamedAttribute[] Attributes { get; }
        /// <summary>
        /// Gets all custom attributes attached to this object
        /// </summary>
        string[] CustomAttributeKeys { get; }
        /// <summary>
        /// This method is called to notify the object of an attribute that changed some of 
        /// its properties. The objects returns true, if it needs to be repainted.
        /// </summary>
        /// <param name="attribute"></param>
        bool AttributeChanged(INamedAttribute attribute);
        /// <summary>
        /// Notifies about the begin and end of a modification with the mouse
        /// </summary>
        /// <param name="sender">the causer</param>
        /// <param name="propertyName">name of the property beeing changed</param>
        /// <param name="startModify">true: beginning, false: ending</param>
        /// <returns></returns>
        bool ModifyWithMouse(object sender, string propertyName, bool startModify);
        /// <summary>
        /// Asks the object to enumerate all its possible snap points according to the 
        /// required modes defined by the parameter.
        /// </summary>
        /// <param name="spf">definition and collection of snap points</param>
        void FindSnapPoint(SnapPointFinder spf);
        /// <summary>
        /// Determins whether the GeoObject has valid data (e.g. to be added to a model).
        /// E.g. a line where the startpoint is identical to the endpoint or a circle with 
        /// radius &lt;=0.0 is considered invalid.
        /// </summary>
        /// <returns>true, if valid, false otherwise</returns>
        bool HasValidData();
        /// <summary>
        /// Returns a description of the GeoObject which is used in the control center
        /// </summary>
        /// <returns>a short description</returns>
        string Description { get; }
        /// <summary>
        /// Called before PaintTo3D, not collected in the DisplayList
        /// </summary>
        /// <param name="paintTo3D"></param>
        void PrePaintTo3D(IPaintTo3D paintTo3D);
        /// <summary>
        /// Paint the object to the 3D display machine and returns a (possibly cached) displaylist
        /// </summary>
        /// <param name="paintTo3D">where to paint</param>
        // IPaintTo3DList GetDisplayList(IPaintTo3D paintTo3D); // wird ersetzt durch folgendes:
        void PaintTo3D(IPaintTo3D paintTo3D);
        void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists);
        /// <summary>
        /// This method will be called from a background thread when a higher precision
        /// displaylist is needed. The object should do all the necessary calculation
        /// to produce a display list with the required precision. The display list 
        /// will later be acquired by a call to PaintTo3DList (or PaintTo3D) from the main thred
        /// because the display dirvers are not multithread enabled.
        /// </summary>
        /// <param name="precision">The required precision</param>
        void PrepareDisplayList(double precision);
        int UniqueId { get; }
        Style.EDefaultFor PreferredStyle { get; }
        BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision);
        IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision);
        IGeoObject[] OwnedItems { get; }
        bool IsVisible { get; set; }
        /// <summary>
        /// Propagates the layer and color from the owner (usually a <see cref="BlockRef"/>) to this object.
        /// </summary>
        /// <param name="layer">The layer</param>
        /// <param name="colorDef">The color</param>
        void PropagateAttributes(Layer layer, ColorDef colorDef);
        GeoObjectList Decompose();
        /// <summary>
        /// Gets or sets an actuator or drive that defines the mechanical constraint or degree of freedom for this object
        /// Used for animation, static objects dont have an actuator (null)
        /// </summary>
        IDrive Actuator { get; set; }
    }

    internal class GeoObjectComparer : IEqualityComparer<IGeoObject>
    {
        public GeoObjectComparer()
        {
        }
        #region IEqualityComparer<IGeoObject> Members

        bool IEqualityComparer<IGeoObject>.Equals(IGeoObject x, IGeoObject y)
        {
            return x.UniqueId == y.UniqueId;
        }

        int IEqualityComparer<IGeoObject>.GetHashCode(IGeoObject obj)
        {
            return obj.UniqueId;
        }

        #endregion
    }

    /// <summary>
    /// Exception type thrown by GeoObjects
    /// </summary>

    public class GeoObjectException : System.ApplicationException
    {
        /// <summary>
        /// Kind of exception
        /// </summary>
        public enum tExceptionType
        {
            /// <summary>
            /// This GeoObject has no children
            /// </summary>
            NoChildren,
            /// <summary>
            /// The property page is invalid
            /// </summary>
            InvalidPropertyPage
        };
        /// <summary>
        /// Gets the kind of exception
        /// </summary>
        public tExceptionType ExceptionType;
        internal GeoObjectException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// This class helps to implement IGeoObject by implementing some IGeoObject methods
    /// in a default way and by offering some helper methods.
    /// </summary>
    public abstract class IGeoObjectImpl : IGeoObject,
        ISerializable, IFeedBack, ICloneable
    {
#if DEBUG
        internal static int InstanceCounter = 0;
        class DebugCommandHandler : ICommandHandler
        {
            IGeoObjectImpl go;
            public DebugCommandHandler(IGeoObjectImpl go)
            {
                this.go = go;
            }

            bool ICommandHandler.OnCommand(string MenuId)
            {
                if (MenuId == "MenuId.ToggleDebugFlag")
                {
                    if (go.UserData.Contains("DebugFlag")) go.UserData.Remove("DebugFlag");
                    else go.UserData.Add("DebugFlag", true);
                    return true;
                }
                return false;
            }

            bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
            {
                if (MenuId == "MenuId.ToggleDebugFlag")
                {
                    CommandState.Checked = go.UserData.Contains("DebugFlag");
                    CommandState.Radio = true;
                    return true;
                }
                return false;
            }
            void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        }
#endif
        private Layer layer;
        private Style style;
        private SortedList userAttributes;
        private bool visible;
        private IDrive actuator;

        /// <summary>
        /// Each call to <see cref="FireWillChange"/> increments this value, each call to
        /// <see cref="FireDidChange"/> decrements this value. If isChanging is 0
        /// then the object is in a stable state.
        /// </summary>
        protected int isChanging; // wenn >0, dann läuft gerade eine Änderung von WillChage-DidChange
        /// <summary>
        /// An ApplicationException thrown when an error happens during a change operation.
        /// </summary>
        protected class ChangingException : ApplicationException
        {
            internal ChangingException(string message)
                : base(message)
            {
            }
        }
        /// <summary>
        /// Helper class to wrap the setting of an attribute. Usually used in this way:
        /// <code>
        ///     using (new ChangingAttribute(this,atrName,attribute))
        ///     {
        ///         // ... set the attribute here ...
        ///     }
        /// </code>
        /// This will call <see cref="FireWillChange"/> before the attribute is changed
        /// and <see cref="FireDidChange"/> after the attribute was changed.
        /// </summary>
        protected class ChangingAttribute : Changing
        {
            /// <summary>
            /// Creates a ChangingAttribute
            /// </summary>
            /// <param name="geoObject">causing GeoObject</param>
            /// <param name="propertyName">name of the property to set the attribute</param>
            /// <param name="propertyValue">the attribute beeing set</param>
            public ChangingAttribute(IGeoObjectImpl geoObject, string propertyName, object propertyValue)
                :
                base(geoObject, false, true, propertyName, propertyValue)
            {
            }
        }
        public static IDisposable MakeChange(IGeoObjectImpl geoObject, string propertyName)
        {
            return new Changing(geoObject, propertyName);
        }
        /// <summary>
        /// Helper class to wrap any changing of this GeoObject. Usually used in this way:
        /// <code>
        ///     using (new Changing(this,...))
        ///     {
        ///         // ... make the change here ...
        ///     }
        /// </code>
        /// This will call <see cref="FireWillChange"/> before the GeoObject is changed
        /// and <see cref="FireDidChange"/> after the GeoObject was changed with the appropriate parameters.
        /// Changig takes nested changes into account and raises the events only at the outermost level.
        /// </summary>
        protected class Changing : IDisposable
        {
            private IGeoObjectImpl geoObject; // Rückverweis
            private GeoObjectChange Change;
            private PropertyInfo FindProperty(object o, string propname)
            {
                PropertyInfo propertyInfo = o.GetType().GetProperty(propname);
                if (propertyInfo == null)
                {   // speziell für ERSACAD: dort haben wg. der flexiblen Namensvergabe die Properties manchmal
                    // einen prefix, der nicht entsprechend geändert wird. Hier wird, wenn eine passende Property
                    // nicht gefunden wird, eine solche gesucht, die mit dem namen endet. Ggf könnte man auch den Typ sicherstellen
                    PropertyInfo[] props = o.GetType().GetProperties();
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (props[i].Name.EndsWith(propname, true, CultureInfo.InvariantCulture))
                        {
                            propertyInfo = props[i];
                            break;
                        }
                    }
                }
                return propertyInfo;
            }
            /// <summary>
            /// Sets <see cref="GeoObjectChange.NoUndoNecessary"/> to true for the
            /// parameter of the call to <see cref="FireWillChange"/> 
            /// and <see cref="FireDidChange"/>.
            /// </summary>
            public void NoUndoNecessary()
            {
                Change.NoUndoNecessary = true;
            }
            /// <summary>
            /// Changing a property, that has public get and set methods, and has only
            /// one property with this name. On construction the value of that property
            /// will be requested by a reflection call to "get_PropertyName"
            /// </summary>
            /// <param name="geoObject">The GeoObject</param>
            /// <param name="PropertyName">The Name of the property</param>
            public Changing(IGeoObjectImpl geoObject, string PropertyName)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    PropertyInfo propertyInfo = FindProperty(geoObject, PropertyName);
                    if (propertyInfo == null) throw new ChangingException("unable to find property " + PropertyName);
                    MethodInfo mi = propertyInfo.GetGetMethod();
                    if (mi == null) throw new ChangingException("unable to find get-property " + PropertyName);
                    Change = new GeoObjectChange(geoObject, PropertyName, mi.Invoke(geoObject, new object[0]));
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing a GeoObject with a specification on how to undo that change.
            /// Undo might be performed later vie reflection. So we need the name of the method
            /// here and the parameters for that method.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            /// <param name="noUndo">true if no undo is required, false otherwise</param>
            /// <param name="onlyAttribute">true if only an attribute is changed not the geometry</param>
            /// <param name="MethodOrPropertyName">name of a public method that might be called later by undo</param>
            /// <param name="Parameters">parameters for the method</param>
            public Changing(IGeoObjectImpl geoObject, bool noUndo, bool onlyAttribute, string MethodOrPropertyName, params object[] Parameters)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, MethodOrPropertyName, Parameters);
                    Change.NoUndoNecessary = noUndo;
                    Change.OnlyAttributeChanged = onlyAttribute;
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing geometrical aspects of an GeoObject. A <see cref="Clone"/> of this GeoObject is 
            /// made and saved and <see cref="CopyGeometry"/> might be called with that clone later
            /// when there is an undo.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            public Changing(IGeoObjectImpl geoObject)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, "CopyGeometry", geoObject.Clone());
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing geometrical aspects of an GeoObject. A <see cref="Clone"/> of this GeoObject is 
            /// made and saved and <see cref="CopyGeometry"/> might be called with that clone later
            /// when there is an undo.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            public Changing(IGeoObjectImpl geoObject, bool UndoNecessary, bool onlyAttribute)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, "CopyGeometry", geoObject.Clone());
                    if (!UndoNecessary) NoUndoNecessary();
                    Change.OnlyAttributeChanged = onlyAttribute;
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing geometrical aspects of an GeoObject. A <see cref="Clone"/> of this GeoObject is 
            /// made and saved and <see cref="CopyGeometry"/> might be called with that clone later
            /// when there is an undo.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            /// <param name="UndoNecessary">true, if undo is required</param>
            public Changing(IGeoObjectImpl geoObject, bool UndoNecessary)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, "CopyGeometry", geoObject.Clone());
                    if (!UndoNecessary) NoUndoNecessary();
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing a GeoObject with a specification on how to undo that change.
            /// Undo might be performed later vie reflection. So we need the name of the method
            /// here and the parameters for that method.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            /// <param name="MethodOrPropertyName">name of a public method that might be called later by undo</param>
            /// <param name="Parameters">parameters for the method</param>
            public Changing(IGeoObjectImpl geoObject, string MethodOrPropertyName, params object[] Parameters)
            {
#if DEBUG
                if (geoObject.UniqueId == 9093)
                {
                    if (geoObject is Ellipse)
                    {

                    }
                }
#endif
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, MethodOrPropertyName, Parameters);
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Changing a GeoObject with a specification on how to undo that change.
            /// Undo might be performed later vie reflection. So we need the name of the method
            /// here and the parameters for that method. The method must be in the given interface.
            /// </summary>
            /// <param name="geoObject">the GeoObject</param>
            /// <param name="interfaceForMethod">the type of the interface which contains the method</param>
            /// <param name="MethodOrPropertyName">name of a public method that might be called later by undo</param>
            /// <param name="Parameters">parameters for the method</param>
            public Changing(IGeoObjectImpl geoObject, Type interfaceForMethod, string MethodOrPropertyName, params object[] Parameters)
            {
                ++geoObject.isChanging;
                this.geoObject = geoObject;
                if (geoObject.isChanging <= 1)
                {
                    Change = new GeoObjectChange(geoObject, interfaceForMethod, MethodOrPropertyName, Parameters);
                    geoObject.FireWillChange(Change);
                }
            }
            /// <summary>
            /// Implements IDisposable.Dispose. Calls <see cref="FireDidChange"/> when not inside a nested
            /// changing.
            /// </summary>
            public virtual void Dispose()
            {
                --geoObject.isChanging;
                if (geoObject.isChanging == 0)
                {
                    //geoObject.paintFacesCache.Clear();
                    //geoObject.paintCurvesCache.Clear();
                    geoObject.FireDidChange(Change);
                }
            }
        }
        internal static int UniqueIdCounter = 0;
        private int uniqueId;
        /// <summary>
        /// Constructor that initializes some members. Must always be called
        /// </summary>
        protected IGeoObjectImpl()
        {
#if DEBUG
            ++InstanceCounter;
#endif
            userData = new UserData();
            //paintFacesCache = new Dictionary<IPaintTo3D, IPaintTo3DList>();
            //paintCurvesCache = new Dictionary<IPaintTo3D, IPaintTo3DList>();
            isModifyingWithMouse = false;
            uniqueId = ++UniqueIdCounter; // kann der Counter überlaufen? Wie geht das Increment mit Überlauf?
            visible = true;
#if DEBUG
            if (5845 == uniqueId)
            {

            }
#endif
        }
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected IGeoObjectImpl(SerializationInfo info, StreamingContext context)
            : this()
        {
            //userData = info.GetValue("UserData", typeof(UserData)) as UserData;
            //layer = InfoReader.Read(info, "Layer", typeof(Layer)) as Layer;
            //style = InfoReader.Read(info, "Style", typeof(Style)) as Style;
            //try
            //{
            //    userAttributes = InfoReader.Read(info, "UserAttributes", typeof(SortedList)) as SortedList;
            //}
            //catch (SerializationException)
            //{
            //    userAttributes = null;
            //}
            //try
            //{
            //    actuator = info.GetValue("Actuator", typeof(IDrive)) as IDrive;
            //}
            //catch (SerializationException)
            //{
            //    actuator = null;
            //}

            // Exception hier ist katastrophal fürs Einlesen (Performance) mit Tabelle viel schneller
            // leider gibt es kein "info.Contains"
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    case "UserData":
                        userData = e.Value as UserData;
                        break;
                    case "Layer":
                        layer = e.Value as Layer;
                        break;
                    case "Style":
                        style = e.Value as Style;
                        break;
                    case "UserAttributes":
                        userAttributes = e.Value as SortedList;
                        break;
                    case "Actuator":
                        actuator = e.Value as IDrive;
                        break;
                }
            }

            // visible wird nicht gespeichert, soll nur währen der Sitzung gelten
            //try
            //{
            //    visible = info.GetBoolean("Visible");
            //}
            //catch (SerializationException)
            //{
            //    visible = true;
            //}
        }
        protected IGeoObjectImpl(StreamingContext context)
            : this()
        {   // nur für das tabellarische Einlesen
        }

        protected void SetSerializationValue(string Name, object Value)
        {
            switch (Name)
            {
                case "UserData":
                    userData = Value as UserData;
                    break;
                case "Layer":
                    layer = Value as Layer;
                    break;
                case "Style":
                    style = Value as Style;
                    break;
                case "UserAttributes":
                    userAttributes = Value as SortedList;
                    break;
                case "Actuator":
                    actuator = Value as IDrive;
                    break;
            }
        }
#if DEBUG
        ~IGeoObjectImpl()
        {
            --InstanceCounter;
        }
#endif
        public int UniqueId { get { return uniqueId; } }
        /// <summary>
        /// Event that is raised when the GeoObject is about to change. 
        /// </summary>
        public event ChangeDelegate WillChangeEvent;
        /// <summary>
        /// Event that is raised when the GeoObject did change. 
        /// </summary>
        public event ChangeDelegate DidChangeEvent;
        public event FeedBackChangedDelegate FeedBackChangedEvent;
        /// <summary>
        /// Helper method to rais the <see cref="WillChangeEvent"/>.
        /// </summary>
        /// <param name="Change">type of chage that is about to happen</param>
        protected void FireWillChange(GeoObjectChange Change)
        {
            if (WillChangeEvent != null) WillChangeEvent(this, Change);
        }
        /// <summary>
        /// Helper method to raise the <see cref="DidChangeEvent"/>.
        /// </summary>
        /// <param name="Change">type of chage that did happen</param>
        protected void FireDidChange(GeoObjectChange Change)
        {
            if (DidChangeEvent != null) DidChangeEvent(this, Change);
            if (FeedBackChangedEvent != null) FeedBackChangedEvent(this);
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.Modify"/>. Must be implemented by each GeoObject.
        /// No default implementation.
        /// </summary>
        /// <param name="m">the modification</param>
        public abstract void Modify(ModOp m);
        /// <summary>
        /// Modify with the inverse modification. Calls Modify(m.GetInverse());
        /// </summary>
        /// <param name="m"></param>
        public virtual void ModifyInverse(ModOp m)
        {
            Modify(m.GetInverse());
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.Clone"/>. Must be implemented by each GeoObject. No default implementation.
        /// </summary>
        /// <returns></returns>
        public abstract IGeoObject Clone();
        /// <summary>
        /// Overrides <see cref="IGeoObject.HasChildren"/>. The deafult implementation returns false.
        /// </summary>
        /// <returns></returns>
        public virtual bool HasChildren()
        {
            return false;
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.NumChildren"/>. The default implementation returns 0.
        /// </summary>
        public virtual int NumChildren
        {
            get
            {
                return 0;
            }
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.Child"/>. The default implementation throws
        /// a <see cref="GeoObjectException"/> <see cref="GeoObjectException.tExceptionType.NoChildren"/>.
        /// </summary>
        /// <param name="Index"></param>
        /// <returns></returns>
        public virtual IGeoObject Child(int Index)
        {
            throw new GeoObjectException(GeoObjectException.tExceptionType.NoChildren);
        }
        private IGeoObjectOwner owner;
        /// <summary>
        /// Overrides <see cref="IGeoObject.Owner"/>. Fully implements set and get property
        /// and saves the value in a private member.
        /// </summary>
        public virtual IGeoObjectOwner Owner
        {
            get
            {
                return owner;
            }
            set
            {
                owner = value;
            }
        }
        /// <summary>
        /// /// Overrides <see cref="IGeoObject.IsAttributeUsed"/>. The default implementation recursively
        /// calls IsAttributeUsed for all children (if any) and then checks the usage for all
        /// attributes implemented by CADability. No checks are performed for non CADability
        /// attributes. So you can override this method to handle your own attributes and call
        /// the base implementation for CADability attributes.
        /// </summary>
        /// <param name="attribute">attribut to check</param>
        /// <returns>true if used by this GeoObject</returns>
        public virtual bool IsAttributeUsed(object attribute)
        {
            if (attribute == null) return false;
            if (layer == attribute) return true;
            for (int i = 0; i < NumChildren; ++i)
            {
                if (Child(i).IsAttributeUsed(attribute)) return true;
            }
            if (this is IColorDef && attribute == (this as IColorDef).ColorDef)
            {
                return true;
            }
            if (this is ILineWidth && attribute == (this as ILineWidth).LineWidth)
            {
                return true;
            }
            if (this is ILinePattern && attribute == (this as ILinePattern).LinePattern)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.AttributeChanged"/>. Checks all CADability attributes.
        /// Checking your own attributes is left to your code.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public virtual bool AttributeChanged(INamedAttribute attribute)
        {	// wird aufgerufen, wenn die Eigenschaft eine Attributes geändert wurde (also z.B.
            // der RGB Wert von ColorDef oder die Sichtbarkeit eines Layers, nicht wenn
            // z.B. die Eigenschaft "Layer" dieses Objektes geändert wurde.
            if (attribute == layer) // Layer haben alle ist ja hier in IGeoObjectimpl definiert
            {
                using (new Changing(this, true, true, "AttributeChanged", attribute))
                {
                    return true;
                }
            }
            if (this is IColorDef)
            {
                if (attribute == (this as IColorDef).ColorDef)
                {
                    using (new Changing(this, true, true, "AttributeChanged", attribute))
                    {
                        return true;
                    }
                }
            }
            if (this is ILineWidth)
            {
                if (attribute == (this as ILineWidth).LineWidth)
                {
                    using (new Changing(this, true, true, "AttributeChanged", attribute))
                    {
                        return true;
                    }
                }
            }
            if (this is ILinePattern)
            {
                if (attribute == (this as ILinePattern).LinePattern)
                {
                    using (new Changing(this, true, true, "AttributeChanged", attribute))
                    {
                        return true;
                    }
                }
            }
            for (int i = 0; i < NumChildren; ++i)
            {
                if (Child(i).AttributeChanged(attribute)) return true;
            }

            return false;
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.CopyGeometry"/>, but doesn't implement it.
        /// Must be implemented by each GeoObject.
        /// </summary>
        /// <param name="ToCopyFrom">GeoObject to copy geometrical data from</param>
        public abstract void CopyGeometry(IGeoObject ToCopyFrom);
        #region Handling Attributes
        /// <summary>
        /// Implements <see cref="IGeoObject.CopyAttributes"/>. The default implementation handles
        /// all CADability attributes and leaves the handling of non CADability attributes to your code.
        /// </summary>
        /// <param name="ToCopyFrom">GeoObject to copy attribute data from</param>
        public virtual void CopyAttributes(IGeoObject ToCopyFrom)
        {
            if (this == ToCopyFrom) return;
            this.visible = ToCopyFrom.IsVisible; // erst am 20.5.10 zugefügt
            this.style = ToCopyFrom.Style; // am Anfang oder am Ende?
            if (ToCopyFrom is IColorDef)
            {
                IColorDef cdTo = this as IColorDef;
                IColorDef cdFrom = ToCopyFrom as IColorDef;
                if (cdTo != null) cdTo.SetTopLevel(cdFrom.ColorDef, true); // geändert wg: Block mit Solid auflösen, Farben von Faces ändern sich
            }
            if (ToCopyFrom is ILinePattern)
            {
                ILinePattern cdTo = this as ILinePattern;
                ILinePattern cdFrom = ToCopyFrom as ILinePattern;
                if (cdTo != null) cdTo.LinePattern = cdFrom.LinePattern;
            }
            if (ToCopyFrom is ILineWidth)
            {
                ILineWidth cdTo = this as ILineWidth;
                ILineWidth cdFrom = ToCopyFrom as ILineWidth;
                if (cdTo != null) cdTo.LineWidth = cdFrom.LineWidth;
            }
            this.Layer = ToCopyFrom.Layer;
            // ob Userdata.Clone hier stehen soll ist nicht ganz klar
            // eigeltich in jedem Clone(), aber von dort aus wird CopyAttributes()
            // aufgerufen, und von wo sonst noch?
            this.UserData.CloneFrom(ToCopyFrom.UserData);
            // erstmal entfernt, da die DEbugId überschrieben wird und das ist blöd zum Debuggen
        }
        private void CollectAttributes(List<INamedAttribute> attributes)
        {
            attributes.Add(layer);
            if (this is IColorDef)
                attributes.Add((this as IColorDef).ColorDef);
            if (this is ILineWidth)
                attributes.Add((this as ILineWidth).LineWidth);
            if (this is ILinePattern)
                attributes.Add((this as ILinePattern).LinePattern);
            if (this is IHatchStyle)
                attributes.Add((this as IHatchStyle).HatchStyle);
            if (this is IDimensionStyle)
                attributes.Add((this as IDimensionStyle).DimensionStyle);
            if (this is IStyle)
                attributes.Add((this as IStyle).Style);
            if (userAttributes != null)
            {
                foreach (INamedAttribute na in userAttributes.Values)
                    attributes.Add(na);
            }
            for (int i = 0; i < NumChildren; i++)
            {
                if (Child(i) is IGeoObjectImpl) (Child(i) as IGeoObjectImpl).CollectAttributes(attributes);
            }
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.Attributes"/>. Returns a collection af all CADability attributes
        /// used by this GeoObject. Non CADability attributes must be handled by your code.
        /// </summary>
        public INamedAttribute[] Attributes
        {
            get
            {
                //ArrayList attributes = new ArrayList();
                //attributes.Add(layer);
                //if (this is IColorDef)
                //    attributes.Add((this as IColorDef).ColorDef);
                //if (this is ILineWidth)
                //    attributes.Add((this as ILineWidth).LineWidth);
                //if (this is ILinePattern)
                //    attributes.Add((this as ILinePattern).LinePattern);
                //if (this is IHatchStyle)
                //    attributes.Add((this as IHatchStyle).HatchStyle);
                //if (this is IDimensionStyle)
                //    attributes.Add((this as IDimensionStyle).DimensionStyle);
                //if (this is IStyle)
                //    attributes.Add((this as IStyle).Style);
                //if (userAttributes != null)
                //{
                //    foreach (INamedAttribute na in userAttributes)
                //        attributes.Add(na);
                //}
                //return attributes.ToArray(typeof(INamedAttribute)) as INamedAttribute[];

                // NEU: sammelt jetzt auch die Attribute der Kinder
                List<INamedAttribute> attributes = new List<INamedAttribute>();
                CollectAttributes(attributes);
                return attributes.ToArray();
            }
        }
        public virtual string[] CustomAttributeKeys
        {
            get
            {
                if (userAttributes != null && userAttributes.Count > 0)
                {
                    string[] res = new string[userAttributes.Count];
                    userAttributes.Keys.CopyTo(res, 0);
                    return res;
                }
                return new string[0];
            }
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.SetNamedAttribute"/>. The default implementation handles
        /// all CADability attributes and leaves the handling of non CADability attributes to your code.
        /// </summary>
        /// <param name="key">key or typename of attribute to set</param>
        /// <param name="toSet">attribute</param>
        public void SetNamedAttribute(string key, INamedAttribute toSet)
        {
            switch (key)
            {
                case "Layer":
                    this.Layer = toSet as Layer;
                    return;
                case "ColorDef":
                    if (this is IColorDef)
                    {
                        (this as IColorDef).ColorDef = toSet as ColorDef;
                        return;
                    }
                    break;
                case "LineWidth":
                    if (this is ILineWidth)
                    {
                        (this as ILineWidth).LineWidth = toSet as LineWidth;
                        return;
                    }
                    break;

                case "LinePattern":
                    if (this is ILinePattern)
                    {
                        (this as ILinePattern).LinePattern = toSet as LinePattern;
                        return;
                    }
                    break;

                case "HatchStyle":
                    if (this is IHatchStyle)
                    {
                        (this as IHatchStyle).HatchStyle = toSet as HatchStyle;
                        return;
                    }
                    break;

                case "DimensionStyle":
                    if (this is IDimensionStyle)
                    {
                        (this as IDimensionStyle).DimensionStyle = toSet as DimensionStyle;
                        return;
                    }
                    break;
                case "Style":
                    if (this is IStyle)
                    {
                        (this as IStyle).Style = toSet as Style;
                        return;
                    }
                    break;
                default:
                    // wenn z.B. die LineWidth für ein Objekt wie Hatch gesetzt wird, also ein nicht passendes Attribut
                    // dann soll nichts passieren. Nur echte neue Namen für keys sollen gesetzt werden können
                    if (userAttributes == null) userAttributes = new SortedList();
                    userAttributes[key] = toSet;
                    return;
            }
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.GetNamedAttribute"/>. The default implementation handles
        /// all CADability attributes and leaves the handling of non CADability attributes to your code.
        /// </summary>
        /// <param name="key">key or typename of attribute to set</param>
        /// <returns>the named attribute</returns>
        public virtual INamedAttribute GetNamedAttribute(string key)
        {
            switch (key)
            {
                case "Layer":
                    return this.Layer;
                case "ColorDef":
                    if (this is IColorDef)
                        return (this as IColorDef).ColorDef;
                    break;
                case "LineWidth":
                    if (this is ILineWidth)
                        return (this as ILineWidth).LineWidth;
                    break;

                case "LinePattern":
                    if (this is ILinePattern)
                        return (this as ILinePattern).LinePattern;
                    break;

                case "HatchStyle":
                    if (this is IHatchStyle)
                        return (this as IHatchStyle).HatchStyle;
                    break;

                case "DimensionStyle":
                    if (this is IDimensionStyle)
                        return (this as IDimensionStyle).DimensionStyle;
                    break;
                case "Style":
                    if (this is IStyle)
                        return (this as IStyle).Style;
                    break;
            }
            if (userAttributes != null)
                return userAttributes[key] as INamedAttribute;
            return null;
        }
        #endregion
        /// <summary>
        /// Implements ISerializable.GetObjectData. Saves <see cref="UserData"/>, <see cref="Layer"/> and <see cref="Style"/>.
        /// All other properties of the GeoObject must be saved by the derived class. don't forget
        /// to call the base implementation
        /// </summary>
        /// <param name="info">info</param>
        /// <param name="context">context</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("UserData", userData);
            info.AddValue("Layer", layer);
            info.AddValue("Style", style);
            info.AddValue("UserAttributes", userAttributes);
            info.AddValue("Actuator", actuator);
        }
        /// <summary>
        /// Should be overridden and return a <see cref="IPropertyEntry"/> derived object
        /// that handles the display and modification of the properties of the IGeoObject derived object.
        /// Default implementation return null.
        /// </summary>
        public virtual IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return null;
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.GetAttributeProperties"/>. The default implementation returns
        /// ShowProperties for all CADability attributes.
        /// </summary>
        /// <param name="Frame">the frame of the view</param>
        /// <returns>list of IPropertyEntry</returns>
        public virtual IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            List<IPropertyEntry> res = new List<IPropertyEntry>();
            // zuerst seperator, evtl. durch boolen steuern?
            res.Add(new SeperatorProperty("GeoObject.Attributes"));
            res.Add(new StyleSelectionProperty(this, "StyleSelection", Frame.Project.StyleList));
            res.Add(new LayerSelectionProperty(this, "LayerSelection", Frame.Project.LayerList));
            if (this is IColorDef)
            {
                res.Add(new ColorSelectionProperty(this as IColorDef, "ColorSelection", Frame.Project.ColorList, ColorList.StaticFlags.allowAll));
            }
            if (this is ILineWidth)
            {
                LineWidthSelectionProperty lws = new LineWidthSelectionProperty("LineWidth.Selection", Frame.Project.LineWidthList, this as ILineWidth, false);
                res.Add(lws);
            }
            if (this is ILinePattern)
            {
                LinePatternSelectionProperty lws = new LinePatternSelectionProperty("LinePatternSelection", Frame.Project.LinePatternList, this as ILinePattern, false);
                res.Add(lws);
            }
            if (userAttributes != null)
            {
                foreach (DictionaryEntry de in userAttributes)
                {
                    IPropertyEntry sp = (de.Value as INamedAttribute).GetSelectionProperty(de.Key as string, Frame.Project, new GeoObjectList(this));
                    if (sp != null) res.Add(sp);
                }
            }
            if (userData != null)
            {
                string[] entries = userData.AllItems;
                for (int i = 0; i < entries.Length; ++i)
                {
                    object data = userData[entries[i]];
                    if (data is IPropertyEntry)
                    {
                        res.Add(data as IPropertyEntry);
                    }
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.GetBoundingCube"/> abstract.
        /// Must be overridden. 
        /// </summary>
        /// <returns>the bounding cube</returns>
        public abstract BoundingCube GetBoundingCube();
        /// <summary>
        /// Implements <see cref="IGeoObject.UpdateAttributes"/> for all CADability attributes.
        /// Non CADability attributes are not handled and must be handled if there are any.
        /// </summary>
        /// <param name="alc"></param>
        public virtual void UpdateAttributes(IAttributeListContainer alc)
        {
            // ein Objekt soll sich den Attributen in den Listen anpassen.
            // das braucht man z.B. wenn es per DragAndDrop aus einem anderen Zusammenhang kommt
            // oder wenn ein Modell dem projekt zugefügt wird.
            // Die Listen werden gleichzeitig ggf. erweitert, wenn ein Attribut sich nicht darin befindet
            // ACHTUNG: die Listen können hinterher inkonsistent sein in dem Sinn, dass ein
            // Style sich auf ein Layer bezieht, welches nicht in der Liste ist
            // es muss sich also noch auf AttributeListContainer.UpdateLists bezogen werden
            if (style != null)
            {
                Style st = alc.StyleList.Find(style.Name);
                if (st == null)
                {
                    st = (Style)style.Clone();
                    alc.StyleList.Add(st);
                }
                style = st;
            }
            if (layer != null)
            {
                Layer l = alc.LayerList.Find(layer.Name);
                if (l == null)
                {
                    l = layer.Clone();
                    alc.LayerList.Add(l);
                }
                this.Layer = l;
            }
            IColorDef icolorDef = this as IColorDef;
            if (icolorDef != null && icolorDef.ColorDef != null)
            {
                ColorDef c = alc.ColorList.Find(icolorDef.ColorDef.Name);
                if (c == null)
                {
                    c = icolorDef.ColorDef.Clone();
                    alc.ColorList.Add(c);
                }
                // icolorDef.ColorDef = c; dabei gibt es bei Import von step files fehler, deshalb folgendes:
                icolorDef.SetTopLevel(c);
            }
            ILineWidth iLineWidth = this as ILineWidth;
            if (iLineWidth != null && iLineWidth.LineWidth != null)
            {
                LineWidth c = alc.LineWidthList.Find(iLineWidth.LineWidth.Name);
                if (c == null)
                {
                    c = iLineWidth.LineWidth.Clone();
                    alc.LineWidthList.Add(c);
                }
                iLineWidth.LineWidth = c;
            }
            ILinePattern iLinePattern = this as ILinePattern;
            if (iLinePattern != null && iLinePattern.LinePattern != null)
            {
                LinePattern c = alc.LinePatternList.Find(iLinePattern.LinePattern.Name);
                if (c == null)
                {
                    c = iLinePattern.LinePattern.Clone();
                    alc.LinePatternList.Add(c);
                }
                iLinePattern.LinePattern = c;
            }
            IHatchStyle iHatchStyle = this as IHatchStyle;
            if (iHatchStyle != null && iHatchStyle.HatchStyle != null)
            {
                HatchStyle c = alc.HatchStyleList.Find(iHatchStyle.HatchStyle.Name);
                if (c == null)
                {
                    c = iHatchStyle.HatchStyle.Clone();
                    alc.HatchStyleList.Add(c);
                }
                iHatchStyle.HatchStyle = c;
            }
            IDimensionStyle iDimensionStyle = this as IDimensionStyle;
            if (iDimensionStyle != null && iDimensionStyle.DimensionStyle != null)
            {
                DimensionStyle c = alc.DimensionStyleList.Find(iDimensionStyle.DimensionStyle.Name);
                if (c == null)
                {
                    c = iDimensionStyle.DimensionStyle.Clone();
                    alc.DimensionStyleList.Add(c);
                }
                iDimensionStyle.DimensionStyle = c;
            }
            if (this is Block)
            {
                Block blk = this as Block;
                for (int i = 0; i < blk.Count; ++i)
                {
                    blk.Child(i).UpdateAttributes(alc);
                }
            }
            if (this is BlockRef)
            {	// das muss aufgerufen werden, wenn z.B. ein Modell zum Projekt hinzugefügt
                // wird und dieses Modell enthält BlockRef Objekte.
                // Leider wird es dabei zu oft aufgerufen, wenn es nämlich mehrere Instanzen
                // des selben Blockrefs enthält. 
                // Alternativ dazu könnte man in solchen Fällen zuerst eine Liste aller verwendeten
                // BlockRefs erzeugen und dann diesen mechanismus für jeden Blockref genau einmal
                // aufrufen (und natürlich diese Implementierung hier abklemmen)
                (this as BlockRef).ReferencedBlock.UpdateAttributes(alc);
            }
            if (this is Solid)
            {
                foreach (Shell sh in (this as Solid).Shells)
                {
                    sh.UpdateAttributes(alc);
                }
            }
            if (this is Shell)
            {
                foreach (Face fc in (this as Shell).Faces)
                {
                    fc.UpdateAttributes(alc);
                }
            }
            if (this is Face)
            {
                foreach (Edge edge in (this as Face).AllEdges)
                {
                    if (edge.Curve3D != null)
                    {   // Leider werden so alle Kanten zweimal durch die Mühle geschickt, wenn sie von einem Solid oder Shell komme
                        // wir bräuchten noch einen Parameter um das zu vermeiden
                        (edge.Curve3D as IGeoObject).UpdateAttributes(alc);
                    }
                }
            }
        }
        private UserData userData;
        /// <summary>
        /// Fully implements <see cref="IGeoObject.UserData"/>. No need to override.
        /// </summary>
        public virtual UserData UserData { get { return userData; } }
        /// <summary>
        /// depreciated
        /// </summary>
        protected bool isModifyingWithMouse;
        /// <summary>
        /// depreciated
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="propertyName"></param>
        /// <param name="startModify"></param>
        /// <returns></returns>
        public virtual bool ModifyWithMouse(object sender, string propertyName, bool startModify)
        {	// mit WillChange zeigt das Objekt an, dass es mit der Maus verändert werden wird
            // mit DidChange, dass die Mausänderung zuende ist.
            // Leider kommen jetzt danach mehrere Paare FireWillChange/FireDidChange, so dass also
            // FireWillChange zweimal hintereinander kommt.
            // Jetzt erstmal die beiden FireWillChange/FireDidChange auskommentiert. Die Variable
            // isModifyingWithMouse sollte den anderen GeoObjectChangeEvent events mitgegeben werden, so
            // dass der erste WillChange isModifyingWithMouse==true hat, der letzte DidChange hat
            // isModifyingWithMouse==false. Damit müsste Undo zurechkommen
            isModifyingWithMouse = startModify;
            if (startModify)
            {
                // FireWillChange(new GeoObjectChangeEvent(GeoObjectChangeEvent.tChangeType.ModifyWithMouse));
            }
            else
            {
                // FireDidChange(new GeoObjectChangeEvent(GeoObjectChangeEvent.tChangeType.ModifyWithMouse));
            }
            return true; // Rückgabewert z.Z. noch ohne Bedeutung
        }
        /// <summary>
        /// Implements <see cref="IGeoObject.FindSnapPoint"/>, but does nothing. Should be overridden.
        /// </summary>
        /// <param name="spf"></param>
        public virtual void FindSnapPoint(SnapPointFinder spf)
        {
        }
        /// <summary>
        /// Default implementation of IGeoObject.HasValidData. Returns true.
        /// Override if the derived object can decide itself.
        /// </summary>
        /// <returns>true</returns>
        public virtual bool HasValidData() { return true; }
        /// <summary>
        /// Returns a textual description of the GeoObject. Mainly used for debugging purposes
        /// </summary>
        public virtual string Description
        {
            get
            {
                return this.ToString();
            }
        }
        /// <summary>
        /// Patin the object to th <see cref="IPaintTo3D"/> interface. 
        /// </summary>
        /// <param name="paintTo3D">Target for the paint operation</param>
        public abstract void PaintTo3D(IPaintTo3D paintTo3D);
        /// <summary>
        /// Called before <see cref="PaintTo3D"/> is called, should implement the time consuming work for the display (like calculationg the triangulation
        /// of Faces or Polylines of Curves). Not normally used by user code.
        /// </summary>
        /// <param name="precision">Required precision</param>
        public abstract void PrepareDisplayList(double precision);
        /// <summary>
        /// Hier kann man Dinge vor OpenList tun (da sie selbst vielleicht ein OpenList benötigen)
        /// </summary>
        /// <param name="paintTo3D"></param>
        public virtual void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
        }
        //private Dictionary<IPaintTo3D, IPaintTo3DList> paintFacesCache;
        //private Dictionary<IPaintTo3D, IPaintTo3DList> paintCurvesCache;
        private int PushParentSelectionIndex(IPaintTo3D paintTo3D)
        {
            if (owner is Block)
            {
                int n = (owner as Block).PushParentSelectionIndex(paintTo3D);
                return n + 1;
            }
            else if (owner is BlockRef)
            {
                int n = (owner as BlockRef).PushParentSelectionIndex(paintTo3D);
                return n + 1;
            }
            else
                return 0;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.IGeoObject.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public virtual void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {   // muss von komplexen Objekten überschrieben werden, hier nur für linienartige
            PrePaintTo3D(paintTo3D);
            lists.Add(layer, false, true, this);
        }
        //public IPaintTo3DList GetDisplayList(IPaintTo3D paintTo3D)
        //{
        //    if (paintTo3D.SelectMode)
        //    {
        //        PrePaintTo3D(paintTo3D);
        //        paintTo3D.OpenList();
        //        PaintTo3D(paintTo3D); // die Objekte dürfen im SelectMode keine Farben einstellen
        //        return paintTo3D.CloseList();
        //    }
        //    if (paintTo3D.PaintSurfaces)
        //    {
        //        IPaintTo3DList list;
        //        if (!paintFacesCache.TryGetValue(paintTo3D, out list))
        //        {
        //            PrePaintTo3D(paintTo3D);
        //            paintTo3D.OpenList();
        //            int npop = PushParentSelectionIndex(paintTo3D);
        //            PaintTo3D(paintTo3D);
        //            for (int i = 0; i < npop; ++i) paintTo3D.PopSelectionIndex();
        //            list = (paintTo3D.CloseList());
        //            paintFacesCache[paintTo3D] = list;
        //        }
        //        return list;
        //    }
        //    if (paintTo3D.PaintEdges)
        //    {
        //        IPaintTo3DList list;
        //        if (!paintCurvesCache.TryGetValue(paintTo3D, out list))
        //        {
        //            PrePaintTo3D(paintTo3D);
        //            paintTo3D.OpenList();
        //            int npop = PushParentSelectionIndex(paintTo3D);
        //            PaintTo3D(paintTo3D);
        //            for (int i = 0; i < npop; ++i) paintTo3D.PopSelectionIndex();
        //            list = (paintTo3D.CloseList());
        //            paintCurvesCache[paintTo3D] = list;
        //        }
        //        return list;
        //    }
        //    return null; // kommt nicht vor
        //}
        /// <summary>
        /// Returns the preferred style for this objects, see <see cref="Style.EDefaultFor"/>.
        /// </summary>
        public virtual Style.EDefaultFor PreferredStyle
        {
            get
            {
                return (Style.EDefaultFor)0;
            }
        }
        /// <summary>
        /// Returns the 2 dimensional extent of objects for a specific projection.
        /// </summary>
        /// <param name="projection">The projection for which the extent is beeing queried</param>
        /// <param name="extentPrecision">Raw or exact extent</param>
        /// <returns>The 2 dimensional extent.</returns>
        public abstract BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision);
        /// <summary>
        /// Deprecated.
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public abstract IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision);
        /// <summary>
        /// Returns an array of objects owned by this object. E.g. a face is owned by a shell.
        /// </summary>
        public virtual IGeoObject[] OwnedItems
        {
            get
            {
                return null;
            }
        }
        /// <summary>
        /// Reads or writes the visible flag. Invisible objects ar not displayed.
        /// </summary>
        public virtual bool IsVisible
        {
            get
            {
                return visible;
            }
            set
            {
                using (new ChangingAttribute(this, "Visible", visible))
                {
                    visible = value;
                }
            }
        }
        /// <summary>
        /// Sets the provided layer and color to this object and propagates these attributes to the owned objects.
        /// </summary>
        /// <param name="layer">Layer to set</param>
        /// <param name="colorDef">Color to set</param>
        public virtual void PropagateAttributes(Layer layer, ColorDef colorDef)
        {
            if (this is IColorDef && colorDef != null)
            {
                if ((this as IColorDef).ColorDef == null || (this as IColorDef).ColorDef.Source == ColorDef.ColorSource.fromParent)
                {
                    (this as IColorDef).ColorDef = colorDef;
                }
                colorDef = (this as IColorDef).ColorDef; // wozu ?? für die ChildObjekte (s.u.) !!
            }
            if (this.layer == null) this.layer = layer;
            layer = this.layer; // nach unten weiterreichen
            if (OwnedItems != null)
            {
                for (int i = 0; i < OwnedItems.Length; ++i)
                {
                    OwnedItems[i].PropagateAttributes(layer, colorDef);
                }
            }
        }
        /// <summary>
        /// Decomposes this GeoObject into simpler GeoObjects. May return null if there are no simpler objects.
        /// </summary>
        /// <returns></returns>
        public virtual GeoObjectList Decompose()
        {
            return null;
        }
        /// <summary>
        /// Sets or gets the drive assiziated with this GeoObject
        /// </summary>
        public virtual IDrive Actuator
        {
            get { return actuator; }
            set
            {
                using (new ChangingAttribute(this, "IGeoObject.Actuator", actuator))
                {
                    actuator = value;
                    if (actuator != null && !actuator.MovedObjects.Contains(this))
                    {
                        actuator.MovedObjects.Add(this);
                    }
                }
            }
        }

        //{   // muss eliminiert werden und bei den Objekten implementiert werden
        //    // keine I2DRepresentation mehr!
        //    GDIResources gdi = new GDIResources();
        //    gdi.DisplayMode = GDIResources.EDisplayMode.normal;
        //    I2DRepresentation[] rep = this.Get2DRepresentation(projection, gdi);
        //    IQuadTreeInsertable []res = new IQuadTreeInsertable[rep.Length];
        //    return new QuadTreeGeoObject(this, rep);
        //}

        /// <summary>
        /// Hilfsfunktion zum Setzen der Farbe und gleichzeitig die Events feuern
        /// </summary>
        /// <param name="color">Referenz auf die i.a. private Membervariable</param>
        /// <param name="c">der neue Farbwert</param>
        protected void SetColorDef(ref ColorDef color, ColorDef c)
        {
            if (c == null) return;
            using (new ChangingAttribute(this, "ColorDef", color))
            {
                switch (c.Source)
                {
                    /*	case ColorDef.ColorSource.fromLayer:
                            if( layer == null)
                                color = ColorDef.CDfromLayer;
                            else
                                color = layer.GetCDfromLayer();
                            break;
                    */
                    case ColorDef.ColorSource.fromParent:
                        if (owner == null)
                            color = ColorDef.CDfromParent;
                        else
                        {
                            Block bl = owner as Block;
                            if (bl != null) color = bl.GetCDfromParent();
                        }
                        break;
                    default:
                        color = c;
                        break;

                }
            }
        }
        /// <summary>
        /// Fully implements <see cref="ILayer.Layer"/>. Stores the layer in a private member.
        /// Setting raises the <see cref="WillChangeEvent"/> and <see cref="DidChangeEvent"/>.
        /// </summary>
        public virtual Layer Layer
        {
            get
            {
                return layer;
            }
            set
            {
                using (new ChangingAttribute(this, "Layer", layer))
                {
                    layer = value;
                }
            }
        }
        /// <summary>
        /// Fully implements <see cref="IStyle.Style"/>. Stores the style in a private member.
        /// Setting raises the <see cref="WillChangeEvent"/> and <see cref="DidChangeEvent"/>.
        /// </summary>
        public virtual Style Style
        {
            set
            {
                using (new ChangingAttribute(this, "Style", style))
                {
                    style = value;
                    if (style != null) style.Apply(this); // diese senden eingeschachtelte Changings
                }
            }
            get
            {
                return style;
            }
        }
        /// <summary>
        /// Returns true, if the setting of the style of this object is identical to the
        /// setting of the individual attributes, false otherwise
        /// </summary>
        public bool StyleIsValid
        {
            get
            {
                if (style == null) return false;
                return style.Check(this);
            }
        }
        #region IOctTreeInsertable members
        public abstract BoundingCube GetExtent(double precision);
        public abstract bool HitTest(ref BoundingCube cube, double precision);
        public abstract bool HitTest(Projection projection, BoundingRect rect, bool onlyInside);
        public abstract bool HitTest(Projection.PickArea area, bool onlyInside);
        public abstract double Position(GeoPoint fromHere, GeoVector direction, double precision);
        #endregion
        #region Static Helper
        /// <summary>
        /// Returns the bounding rectangle of the GeoObject with respect to a specified projection
        /// </summary>
        /// <param name="go">the GeoObject</param>
        /// <param name="projection">the projection</param>
        /// <param name="regardLineWidth">regard line with</param>
        /// <returns>the bounding rectangle</returns>
        public static BoundingRect GetExtent(IGeoObject go, Projection projection, bool regardLineWidth)
        {
            return go.GetExtent(projection, ExtentPrecision.Raw);
        }
        /// <summary>
        /// Checks whether the GeoObject is owned by a <see cref="BlockRef"/> object.
        /// </summary>
        /// <param name="toTest"></param>
        /// <returns></returns>
        public static bool IsOwnedByBlockRef(IGeoObject toTest)
        {
            IGeoObjectOwner res = toTest.Owner;
            if (res is BlockRef) return true;
            if (res is IGeoObject) return IsOwnedByBlockRef(res as IGeoObject);
            return false;
        }
        public static IDisposable ChangingUserData(IGeoObject involved, string key, object oldValue)
        {   // das macht noch kein Undo bei Userdata, das muss noch implementiert werden
            Changing res = new Changing(involved as IGeoObjectImpl, false, true);
            return res;
        }
        public static IDisposable ChangingUserData(IGeoObject involved, string key, object oldValue, bool onlyAttribute)
        {   // das macht noch kein Undo bei Userdata, das muss noch implementiert werden
            Changing res = new Changing(involved as IGeoObjectImpl, false, onlyAttribute);
            return res;
        }
        #endregion
        #region ICloneable Members

        object ICloneable.Clone()
        {
            return this.Clone();
        }

        #endregion
        int IComparable.CompareTo(object obj)
        {
            if (obj is IGeoObjectImpl) return (obj as IGeoObjectImpl).uniqueId.CompareTo(uniqueId);
            return 0;
        }
        public delegate string GetAdditionalContextMenueDelegate(IGeoObject sender);
        public static event GetAdditionalContextMenueDelegate GetAdditionalContextMenueEvent;
        internal virtual void GetAdditionalContextMenue(IShowProperty sender, IFrame frame, List<MenuWithHandler> toManipulate)
        {
            if (GetAdditionalContextMenueEvent != null)
            {
                string menuId = GetAdditionalContextMenueEvent(this);
                if (menuId != null && menuId.Length > 0)
                {
                    frame.ContextMenuSource = sender as IPropertyEntry;
                    MenuWithHandler menuWithHandler = new MenuWithHandler();
                    menuWithHandler.ID = menuId;
                    MenuWithHandler[] items = MenuResource.LoadMenuDefinition(menuId, false, frame.CommandHandler);
                    toManipulate.AddRange(items);
                    // still need to implement functionality of: MenuResource.AppendContextMenu(toManipulate, menuId, frame.CommandHandler, true);
                }
            }
        }
#if DEBUG
#endif

        BoundingCube IFeedBack.GetExtent()
        {
            return this.GetExtent(1.0);
        }

        public virtual void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("UserData", userData);
            if (layer != null) data.AddProperty("Layer", layer);
            if (style != null) data.AddProperty("Style", style);
            if (userAttributes != null) data.AddProperty("UserAttributes", userAttributes);
            if (actuator != null) data.AddProperty("Actuator", actuator);
        }


        public virtual void SetObjectData(IJsonReadData data)
        {
            userData = data.GetPropertyOrDefault<UserData>("UserData");
            layer = data.GetPropertyOrDefault<Layer>("Layer");
            style = data.GetPropertyOrDefault<Style>("Style");
            userAttributes = data.GetPropertyOrDefault<SortedList>("UserAttributes");
            actuator = data.GetPropertyOrDefault<IDrive>("Actuator");
        }

        protected void JsonSerializationDone()
        {
            throw new NotImplementedException();
        }
#if DEBUG
        public DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(this);
                return dc;
            }
        }
        public bool IsDebug
        {
            get
            {
                return UserData.Contains("DebugFlag");
            }
        }
#endif
    }
    public delegate void CreateContextMenueDelegate(IGeoObjectShowProperty sender, List<MenuWithHandler> toManipulate);


    public interface IGeoObjectShowProperty
    {
        event CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject GetGeoObject();
        string GetContextMenuId();
    }
}
