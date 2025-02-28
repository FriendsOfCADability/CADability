using CADability.GeoObject;
using CADability.Substitutes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// Methods and properties common to all drive objects
    /// </summary>

    public interface IDrive
    {
        string Name { get; set; }
        /// <summary>
        /// Defines an other drive this drive depends on. The movement of the driven objects is
        /// the accumulated by all drives that prepend this drive. If DependsOn is null, then this
        /// drive is absoulte to the fixed world
        /// </summary>
        IDrive DependsOn { get; set; }
        /// <summary>
        /// Gets or sets the current position of the driven objects.
        /// </summary>
        double Position { get; set; }
        /// <summary>
        /// Returns the modification that has to be applied to the objects moved by this drive
        /// to calculate the current position of the objects. This depends on the <see cref="Position"/> that
        /// has been set before (the current position). The modification does not take into account that this drive
        /// depends on another drive.
        /// </summary>
        ModOp Movement { get; }
        List<IGeoObject> MovedObjects { get; }
    }

    [Serializable]
    public class DriveList : IEnumerable<IDrive>, ISerializable, IDeserializationCallback, IJsonSerialize
    {
        static internal bool DependsOn(IDrive toCheck, IDrive dependant)
        {
            if (toCheck.DependsOn == null) return false;
            if (toCheck.DependsOn == dependant) return true;
            return DependsOn(toCheck.DependsOn, dependant);
        }
        private List<IDrive> drives;
        IDrive[] deserialized;
        public DriveList()
        {
            drives = new List<IDrive>();
        }
        public void Add(IDrive toAdd)
        {   // Namensgleichheit überprüfen
            drives.Add(toAdd);
        }
        public void Remove(IDrive drive)
        {
            drives.Remove(drive);
        }
        #region IEnumerable<IDrive> Members
        IEnumerator<IDrive> IEnumerable<IDrive>.GetEnumerator()
        {
            return drives.GetEnumerator();
        }
        #endregion
        #region IEnumerable Members
        IEnumerator IEnumerable.GetEnumerator()
        {
            return drives.GetEnumerator();
        }
        #endregion
        #region ISerializable Members
        protected DriveList(SerializationInfo info, StreamingContext context)
        {
            deserialized = info.GetValue("Drives", typeof(IDrive[])) as IDrive[];
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Drives", drives.ToArray());
        }
        #endregion
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            drives = new List<IDrive>(deserialized);
            deserialized = null;
        }

        #endregion
        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Drives", drives.ToArray());
        }

        public void SetObjectData(IJsonReadData data)
        {
            drives = new List<IDrive>(data.GetProperty<IDrive[]>("Drives"));
        }

        #endregion

        public IDrive Find(string name)
        {
            for (int i = 0; i < drives.Count; ++i)
            {
                if (drives[i].Name == name) return drives[i];
            }
            return null;
        }
    }

    internal class ListWithEvents<T> : List<T>
    {
        public ListWithEvents(IEnumerable<T> e)
            : base(e)
        {
        }
        public ListWithEvents()
            : base()
        {
        }
        public delegate bool AddingDelegate(T item);
        public delegate void AddedDelegate(T item);
        public delegate bool RemovingDelegate(T item);
        public delegate void RemovedDelegate(T item);
        public event AddingDelegate OnAdding;
        public event AddedDelegate OnAdded;
        public event RemovingDelegate OnRemoving;
        public event RemovedDelegate OnRemoved;
        public new void Add(T toAdd)
        {
            if (OnAdding != null) if (!OnAdding(toAdd)) return;
            base.Add(toAdd);
            if (OnAdded != null) OnAdded(toAdd);
        }
        public new void Remove(T toRemove)
        {
            if (OnRemoving != null) if (!OnRemoving(toRemove)) return;
            base.Remove(toRemove);
            if (OnRemoved != null) OnRemoved(toRemove);
        }
    }

    /// <summary>
    /// Describes a movement along a curve (which may be any path or simple curve like a line)
    /// </summary>
    [Serializable]
    public class CurveDrive : PropertyEntryImpl, IDrive, ISerializable, ICommandHandler, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private string name;
        private IDrive dependsOn;
        private ICurve moveAlong;
        private double nullPosition;
        private bool tangential;
        private Plane startingPoint; // Ebene, deren location der Ausgangspunktpunkt auf der Kurve ist
        private ListWithEvents<IGeoObject> movedObjects;
        IGeoObject[] movedObjectsDeserialized;
        private double position;
        private ModOp toPosition;
        internal CurveDrive()
        {   // für interaktiv
            base.resourceIdInternal = "CurveDrive";
            movedObjects = new ListWithEvents<IGeoObject>();
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
        }
        // und deren XAchse die Richtung der Kurve in diesem Punkt

        /// <summary>
        /// Defines a CurveDrive providing the curve for the movement and the object beeing moved.
        /// The currentPosition describes the position of the object as it is defined in the model
        /// relative to the startpoint of the curve. It must be between 0.0 and the
        /// <see cref="ICurve.Length">length</see> of the curve.
        /// </summary>
        /// <param name="along">Curve for the movement</param>
        /// <param name="toMove">Object beeing moved</param>
        /// <param name="currentPosition">Current position of the object</param>
        public CurveDrive(ICurve along, double currentPosition)
            : this()
        {
            moveAlong = along;
            nullPosition = currentPosition;
            InitStartingPoint();
            toPosition = ModOp.Identity;
        }
        private void InitStartingPoint()
        {
            if (moveAlong == null) return;
            double par0 = moveAlong.PositionAtLength(nullPosition);
            GeoPoint location = moveAlong.PointAt(par0);
            GeoVector dirx = moveAlong.DirectionAt(par0);
            if (moveAlong.GetPlanarState() == PlanarState.UnderDetermined)
            {
                tangential = false;
                Plane tmp = new Plane(location, dirx); // irgendeine Senkrechte
                startingPoint = new Plane(location, dirx, tmp.DirectionX ^ dirx);
            }
            else if (moveAlong.GetPlanarState() == PlanarState.Planar)
            {
                tangential = true;
                Plane tmp = moveAlong.GetPlane();
                startingPoint = new Plane(location, dirx, tmp.Normal ^ dirx);
            }
            else
            {   // man könnte in diesem Fall eine nichttangentiale Bewegung machen
                tangential = false;
                startingPoint = new Plane(location, GeoVector.XAxis, GeoVector.YAxis);
            }
        }
        public double NullPosition
        {
            get
            {
                return nullPosition;
            }
            set
            {
                nullPosition = value;
            }
        }
        internal void SyncWithModel(Model m)
        {
            movedObjects = new ListWithEvents<IGeoObject>();
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
            foreach (IGeoObject go in m)
            {
                if (go.Actuator == this)
                {
                    movedObjects.Add(go);
                    go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
                }
            }
        }
        void OnMovedObjectsRemoved(IGeoObject item)
        {
            if (item.Actuator == this) item.Actuator = null;
            item.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            subEntries = null;
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        void OnMovedObjectsAdded(IGeoObject item)
        {
            if (item.Actuator != this) item.Actuator = this;
            item.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
        }
        void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender.Actuator != this)
            {
                movedObjects.Remove(Sender);
            }
        }
        /// <summary>
        /// Gets or sets the tangential movement property. If true, the orientation of the driven
        /// objects will follow the tangent of the curve, if false, the orientation of the driven
        /// objects will stay fixed in space.
        /// </summary>
        public bool Tangential
        {
            get
            {
                return tangential;
            }
            set
            {
                tangential = value;
            }
        }
        public ICurve MoveAlong
        {
            get
            {
                return moveAlong;
            }
            set
            {
                moveAlong = value;
                InitStartingPoint();
            }
        }
        /// <summary>
        /// Returns a <see cref="Shell"/> composed of connected faces that describes the ruled surface
        /// which is described by a line synchronously connecting the two CurveDrives.
        /// </summary>
        /// <param name="wire1">First wire</param>
        /// <param name="wire2">Second wire</param>
        /// <returns>Shell describing the ruled surface</returns>
        public static Shell RuledSurface(CurveDrive wire1, CurveDrive wire2)
        {
            return null;
        }
        public ICurve Curve
        {
            get
            {
                return moveAlong;
            }
        }
        #region IDrive Members
        string IDrive.Name
        {
            set
            {
                name = value;
            }
            get
            {
                return name;
            }
        }
        IDrive IDrive.DependsOn
        {
            get
            {
                return dependsOn;
            }
            set
            {
                dependsOn = value;
            }
        }
        double IDrive.Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
                if (moveAlong == null)
                {
                    toPosition = ModOp.Identity;
                    return;
                }
                if (moveAlong.IsClosed && position > moveAlong.Length)
                {
                    double f = Math.Floor(position / moveAlong.Length);
                    position = position - f * moveAlong.Length;
                }
                double par = moveAlong.PositionAtLength(position);
                GeoPoint location = moveAlong.PointAt(par);
                GeoVector dirx = moveAlong.DirectionAt(par);
                Plane actPosition;
                if (tangential && moveAlong.GetPlanarState() != PlanarState.UnderDetermined)
                {
                    Plane tmp = moveAlong.GetPlane();
                    actPosition = new Plane(location, dirx, tmp.Normal ^ dirx);
                    // viel zu aufwendig, 9 Gleichungen! Verschiebung und Drehung sollte reichen
                    toPosition = ModOp.Transform(startingPoint.CoordSys, actPosition.CoordSys);
                }
                else
                {   // nur Verschiebung
                    toPosition = ModOp.Translate(location - startingPoint.Location);
                    //Plane tmp = new Plane(location, GeoVector.ZAxis); // irgendeine Senkrechte ZAxis ist nicht gut, da möglicherweise parallel
                    //actPosition = new Plane(location, dirx, tmp.DirectionX ^ dirx);
                }
            }
        }
        ModOp IDrive.Movement
        {
            get
            {
                return toPosition;
            }
        }
        List<IGeoObject> IDrive.MovedObjects
        {
            get
            {
                return movedObjects;
            }
        }
        #endregion
        #region ISerializable Members
        protected CurveDrive(SerializationInfo info, StreamingContext context)
            : this()
        {
            name = info.GetString("Name");
            moveAlong = info.GetValue("MoveAlong", typeof(ICurve)) as ICurve;
            nullPosition = info.GetDouble("NullPosition");
            tangential = info.GetBoolean("Tangential");
            startingPoint = (Plane)info.GetValue("StartingPoint", typeof(Plane));
            movedObjectsDeserialized = info.GetValue("MovedObjects", typeof(IGeoObject[])) as IGeoObject[];
            try
            {
                dependsOn = info.GetValue("DependsOn", typeof(IDrive)) as IDrive;
            }
            catch (SerializationException)
            {
                dependsOn = null;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("MoveAlong", moveAlong);
            info.AddValue("NullPosition", nullPosition);
            info.AddValue("Tangential", tangential);
            info.AddValue("StartingPoint", startingPoint);
            info.AddValue("MovedObjects", movedObjects.ToArray());
            info.AddValue("DependsOn", dependsOn);
        }
        #endregion
        #region IPropertyEntry implementation
        public override PropertyEntryType Flags => PropertyEntryType.Selectable | PropertyEntryType.ContextMenu | PropertyEntryType.LabelEditable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override string LabelText
        {
            get
            {
                return name;
            }
            set
            {
                base.LabelText = value;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.CurveDrive", false, this);
            }
        }
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    res.Add(new CurveDriveCurveProperty(this));
                    res.Add(new DoubleProperty(this, "NullPosition", "Drive.NullPosition", this.Frame));
                    res.Add(new BooleanProperty(this, "Tangential", "CurveDrive.Tangential"));
                    res.Add(new GeoObjectListProperty(movedObjects, "Drive.MovedObjects", "MenuId.Drive.MovedObjects", this));
                    List<string> selections = new List<string>();
                    selections.Add(StringTable.GetString("CurveDrive.Independent"));
                    if (propertyPage.ActiveView is AnimatedView)
                    {
                        AnimatedView av = propertyPage.ActiveView as AnimatedView;
                        foreach (IDrive dv in av.DriveList)
                        {
                            if (dv != this && !DriveList.DependsOn(dv, this))
                            {
                                selections.Add(dv.Name);
                            }
                        }
                    }
                    string initial = StringTable.GetString("CurveDrive.Independent");
                    if ((this as IDrive).DependsOn != null) initial = (this as IDrive).DependsOn.Name;
                    MultipleChoiceProperty mcp = new MultipleChoiceProperty("CurveDrive.DependsOn", selections.ToArray(), initial);
                    mcp.ValueChangedEvent += new ValueChangedDelegate(OnDriveChanged);
                    res.Add(mcp);
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnDriveChanged(object sender, object NewValue)
        {
            if (propertyPage.ActiveView is AnimatedView)
            {
                AnimatedView av = propertyPage.ActiveView as AnimatedView;
                (this as IDrive).DependsOn = av.DriveList.Find(NewValue as string);
                foreach (IDrive dv in av.DriveList)
                {
                    if (dv is IPropertyEntry && dv != this)
                    {
                        propertyPage.Refresh(dv as IPropertyEntry);
                    }
                }
            }
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            ShowPropertyDrives spd = propertyPage.GetParent(this) as ShowPropertyDrives;
            if (spd != null)
            {
                if (spd.MayChangeName(this, newValue))
                {
                    (this as IDrive).Name = newValue;
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Refresh ()"/>
        /// </summary>
        public override void Refresh()
        {
            subEntries = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false);
        }
        #endregion
        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("MoveAlong", moveAlong);
            data.AddProperty("NullPosition", nullPosition);
            data.AddProperty("Tangential", tangential);
            data.AddProperty("StartingPoint", startingPoint);
            data.AddProperty("MovedObjects", movedObjects.ToArray());
            data.AddProperty("DependsOn", dependsOn);
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetProperty<string>("Name");
            moveAlong = data.GetProperty<ICurve>("MoveAlong");
            nullPosition = data.GetProperty<double>("NullPosition");
            tangential = data.GetProperty<bool>("Tangential");
            startingPoint = data.GetProperty<Plane>("StartingPoint");
            movedObjects = new ListWithEvents<IGeoObject>(data.GetProperty<IGeoObject[]>("MovedObjects"));
            dependsOn = data.GetProperty<AxisDrive>("DependsOn");
            data.RegisterForSerializationDoneCallback(this);
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
        }
        #endregion

        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDrive.Remove":
                    ShowPropertyDrives spd = propertyPage.GetParent(this) as ShowPropertyDrives;
                    if (spd != null) spd.Remove(this);
                    return true;
                case "MenuId.CurveDrive.Rename":
                    this.StartEdit(false);
                    return true;
                case "MenuId.Drive.MovedObjects.Show":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        List<IGeoObject> list = new List<IGeoObject>(movedObjects);
                        av.SetSelectedObjects(new GeoObjectList(list));
                    }
                    return true;
                case "MenuId.Drive.MovedObjects.Set":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        if (list.Count > 0)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                movedObjects.Add(list[i]);
                            }
                        }
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDrive.Remove":
                    return true;
                case "MenuId.CurveDrive.Rename":
                    return true;
                case "MenuId.Drive.MovedObjects.Show":
                    CommandState.Enabled = (Frame.ActiveView is AnimatedView);
                    return true;
                case "MenuId.Drive.MovedObjects.Set":
                    CommandState.Enabled = false;
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        CommandState.Enabled = list.Count > 0;
                    }
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            movedObjects = new ListWithEvents<IGeoObject>(movedObjectsDeserialized);
            movedObjectsDeserialized = null;
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
            InitStartingPoint();
        }
        #endregion
    }

    [Serializable]
    public class DualCurveDrive : PropertyEntryImpl, IDrive, ICommandHandler, ISerializable, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private string name;
        private CurveDrive drive1;
        private CurveDrive drive2;
        bool stretch;
        private GeoPoint StartPointOnDrive1, StartPointOnDrive2;
        private ListWithEvents<IGeoObject> movedObjects;
        IGeoObject[] movedObjectsDeserialized;
        public DualCurveDrive()
        {
            base.resourceIdInternal = "DualCurveDrive";
            movedObjects = new ListWithEvents<IGeoObject>();
        }
        public DualCurveDrive(CurveDrive drive1, CurveDrive drive2) : this()
        {
            this.drive1 = drive1;
            this.drive2 = drive2;
            movedObjects = new ListWithEvents<IGeoObject>();
            Init();
        }
        public bool Stretch
        {
            get
            {
                return stretch;
            }
            set
            {
                stretch = value;
            }
        }
        internal void Init()
        {
            if (drive1 != null)
            {
                StartPointOnDrive1 = drive1.MoveAlong.PointAt(drive1.NullPosition);
            }
            if (drive2 != null)
            {
                StartPointOnDrive2 = drive2.MoveAlong.PointAt(drive2.NullPosition);
            }
        }
        void OnMovedObjectsRemoved(IGeoObject item)
        {
            if (item.Actuator == this) item.Actuator = null;
            item.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            subEntries = null;
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        void OnMovedObjectsAdded(IGeoObject item)
        {
            if (item.Actuator != this) item.Actuator = this;
            item.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
        }
        void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender.Actuator != this)
            {
                movedObjects.Remove(Sender);
            }
        }

        #region IDrive Members
        string IDrive.Name
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
        IDrive IDrive.DependsOn
        {   // ist immer unabhänig bzw hängt von drive1 und drive2 ab, aber dort ist die Abhängigkeit ja schon drin
            get
            {
                return null;
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }
        double IDrive.Position
        {
            get
            {
                return 0.0; // es gibt keine position, die ist alleine durch die beiden Antriebe bestimmt
            }
            set
            {
                // hier bleibt nichts zu tun
            }
        }
        ModOp IDrive.Movement
        {
            get
            {
                GeoPoint p1 = (drive1 as IDrive).Movement * StartPointOnDrive1;
                GeoPoint p2 = (drive2 as IDrive).Movement * StartPointOnDrive2;
                try
                {
                    ModOp m = ModOp.Fit(new GeoPoint[] { StartPointOnDrive1, StartPointOnDrive2 }, new GeoPoint[] { p1, p2 }, stretch);
                    GeoPoint pp1 = m * StartPointOnDrive1;
                    GeoPoint pp2 = m * StartPointOnDrive2;
                    return m;
                }
                catch (ModOpException)
                {
                    return ModOp.Identity;
                }
            }
        }
        List<IGeoObject> IDrive.MovedObjects
        {
            get
            {
                return movedObjects;
            }
        }
        #endregion
        #region IPropertyEntry implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.LabelEditable;
        public override string LabelText
        {
            get
            {
                return name;
            }
            set
            {
                base.LabelText = value;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.CurveDrive", false, this);
            }
        }
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    res.Add(new GeoObjectListProperty(movedObjects, "Drive.MovedObjects"));
                    List<string> selections = new List<string>();
                    if (propertyPage.ActiveView is AnimatedView)
                    {
                        AnimatedView av = propertyPage.ActiveView as AnimatedView;
                        foreach (IDrive dv in av.DriveList)
                        {
                            if (dv is CurveDrive)
                            {
                                selections.Add(dv.Name);
                            }
                        }
                    }
                    string init = null;
                    if (drive1 != null) init = (drive1 as IDrive).Name;
                    MultipleChoiceProperty mcp1 = new MultipleChoiceProperty("DualCurveDrive.Curve1", selections.ToArray(), init);
                    mcp1.ValueChangedEvent += new ValueChangedDelegate(OnDrive1Changed);
                    res.Add(mcp1);
                    init = null;
                    if (drive2 != null) init = (drive2 as IDrive).Name;
                    MultipleChoiceProperty mcp2 = new MultipleChoiceProperty("DualCurveDrive.Curve2", selections.ToArray(), init);
                    mcp2.ValueChangedEvent += new ValueChangedDelegate(OnDrive2Changed);
                    res.Add(mcp2);
                    BooleanProperty str = new BooleanProperty(this, "Stretch", "DualCurveDrive.Stretch", "DualCurveDrive.StretchValues");
                    res.Add(str);
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnDrive1Changed(object sender, object NewValue)
        {
            AnimatedView av = propertyPage.ActiveView as AnimatedView;
            string newName = NewValue as string;
            foreach (IDrive dv in av.DriveList)
            {
                if (dv.Name == newName)
                {
                    drive1 = dv as CurveDrive;
                    Init();
                    break;
                }
            }
        }
        void OnDrive2Changed(object sender, object NewValue)
        {
            AnimatedView av = propertyPage.ActiveView as AnimatedView;
            string newName = NewValue as string;
            foreach (IDrive dv in av.DriveList)
            {
                if (dv.Name == newName)
                {
                    drive2 = dv as CurveDrive;
                    Init();
                    break;
                }
            }
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            ShowPropertyDrives spd = propertyPage.GetParent(this) as ShowPropertyDrives;
            if (spd != null)
            {
                if (spd.MayChangeName(this, newValue))
                {
                    (this as IDrive).Name = newValue;
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Refresh ()"/>
        /// </summary>
        public override void Refresh()
        {
            subEntries = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false);
        }
        #endregion
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            return false;
            throw new Exception("The method or operation is not implemented.");
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region ISerializable Members
        protected DualCurveDrive(SerializationInfo info, StreamingContext context)
        {
            name = info.GetString("Name");
            drive1 = info.GetValue("Drive1", typeof(CurveDrive)) as CurveDrive;
            drive2 = info.GetValue("Drive2", typeof(CurveDrive)) as CurveDrive;
            movedObjectsDeserialized = info.GetValue("MovedObjects", typeof(IGeoObject[])) as IGeoObject[];
            try
            {
                stretch = info.GetBoolean("Stretch");
            }
            catch (SerializationException)
            {
                stretch = false;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Drive1", drive1);
            info.AddValue("Drive2", drive2);
            info.AddValue("MovedObjects", movedObjects.ToArray());
            info.AddValue("Stretch", stretch);
        }
        #endregion
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            movedObjects = new ListWithEvents<IGeoObject>(movedObjectsDeserialized);
            movedObjectsDeserialized = null;
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
            Init();
        }

        #endregion
        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Drive1", drive1);
            data.AddProperty("Drive2", drive2);
            data.AddProperty("MovedObjects", movedObjects.ToArray());
            data.AddProperty("Stretch", stretch);
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetProperty<string>("Name");
            drive1 = data.GetProperty<CurveDrive>("Drive1");
            drive2 = data.GetProperty<CurveDrive>("Drive2");
            movedObjects = new ListWithEvents<IGeoObject>(data.GetProperty<IGeoObject[]>("MovedObjects"));
            stretch = data.GetProperty<bool>("Stretch");
            data.RegisterForSerializationDoneCallback(this);
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
            Init();
        }
        #endregion
    }

    /// <summary>
    /// Describes a rotation around an axis.
    /// </summary>
    [Serializable]
    public class AxisDrive : PropertyEntryImpl, IDrive, ICommandHandler, IDeserializationCallback, ISerializable, IJsonSerialize, IJsonSerializeDone
    {
        string name;
        GeoPoint location;
        GeoVector direction;
        double position;
        ModOp toPosition;
        private IDrive dependsOn;
        IGeoObject[] movedObjectsDeserialized;
        private ListWithEvents<IGeoObject> movedObjects;
        /// <summary>
        /// Defines a AxisDrive object. The axis and the driven objects must be provided. The
        /// position is meassured in radians.
        /// </summary>
        /// <param name="location">Location of the axis</param>
        /// <param name="direction">Direction of the axis</param>
        /// <param name="currentPosition">Current position of the object</param>
        /// <param name="toRotate">Objects to rotate</param>
        public AxisDrive(GeoPoint location, GeoVector direction, double currentPosition)
        {
            this.location = location;
            this.direction = direction;
            movedObjects = new ListWithEvents<IGeoObject>();
            toPosition = ModOp.Identity;
        }
        #region IDrive Members
        string IDrive.Name
        {
            set
            {
                name = value;
            }
            get
            {
                return name;
            }
        }
        IDrive IDrive.DependsOn
        {
            get
            {
                return dependsOn;
            }
            set
            {
                dependsOn = value;
            }
        }
        double IDrive.Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
                toPosition = ModOp.Rotate(location, direction, Math.PI * 2.0 * position / 360);
            }
        }
        ModOp IDrive.Movement
        {
            get
            {
                return toPosition;
            }
        }
        List<IGeoObject> IDrive.MovedObjects
        {
            get
            {
                return movedObjects;
            }
        }
        #endregion
        void OnMovedObjectsRemoved(IGeoObject item)
        {
            if (item.Actuator == this) item.Actuator = null;
            item.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            subEntries = null;
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        void OnMovedObjectsAdded(IGeoObject item)
        {
            if (item.Actuator != this) item.Actuator = this;
            item.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
        }
        void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender.Actuator != this)
            {
                movedObjects.Remove(Sender);
            }
        }
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDrive.Remove":
                    ShowPropertyDrives spd = propertyPage.GetParent(this) as ShowPropertyDrives;
                    if (spd != null) spd.Remove(this);
                    return true;
                case "MenuId.CurveDrive.Rename":
                    this.StartEdit(false);
                    return true;
                case "MenuId.Drive.MovedObjects.Show":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        List<IGeoObject> list = new List<IGeoObject>(movedObjects);
                        av.SetSelectedObjects(new GeoObjectList(list));
                    }
                    return true;
                case "MenuId.Drive.MovedObjects.Set":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        if (list.Count > 0)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                movedObjects.Add(list[i]);
                            }
                        }
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDrive.Remove":
                    return true;
                case "MenuId.CurveDrive.Rename":
                    return true;
                case "MenuId.Drive.MovedObjects.Show":
                    CommandState.Enabled = (Frame.ActiveView is AnimatedView);
                    return true;
                case "MenuId.Drive.MovedObjects.Set":
                    CommandState.Enabled = false;
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        CommandState.Enabled = list.Count > 0;
                    }
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IPropertyEntry implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.LabelEditable;
        public override string LabelText
        {
            get
            {
                return name;
            }
            set
            {
                base.LabelText = value;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.CurveDrive", false, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>,
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    res.Add(new AxisDriveCurveProperty(this));
                    // res.Add(new DoubleProperty(this, "NullPosition", "Drive.NullPosition", this.Frame));
                    res.Add(new GeoObjectListProperty(movedObjects, "Drive.MovedObjects", "MenuId.Drive.MovedObjects", this));
                    List<string> selections = new List<string>();
                    selections.Add(StringTable.GetString("CurveDrive.Independent"));
                    if (propertyPage.ActiveView is AnimatedView)
                    {
                        AnimatedView av = propertyPage.ActiveView as AnimatedView;
                        foreach (IDrive dv in av.DriveList)
                        {
                            if (dv != this && !DriveList.DependsOn(dv, this))
                            {
                                selections.Add(dv.Name);
                            }
                        }
                    }
                    string initial = StringTable.GetString("CurveDrive.Independent");
                    if ((this as IDrive).DependsOn != null) initial = (this as IDrive).DependsOn.Name;
                    MultipleChoiceProperty mcp = new MultipleChoiceProperty("CurveDrive.DependsOn", selections.ToArray(), initial);
                    mcp.ValueChangedEvent += new ValueChangedDelegate(OnDriveChanged);
                    res.Add(mcp);
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnDriveChanged(object sender, object NewValue)
        {
            if (propertyPage.ActiveView is AnimatedView)
            {
                AnimatedView av = propertyPage.ActiveView as AnimatedView;
                (this as IDrive).DependsOn = av.DriveList.Find(NewValue as string);
                foreach (IDrive dv in av.DriveList)
                {
                    if (dv is IPropertyEntry && dv != this)
                    {
                        propertyPage.Refresh(dv as IPropertyEntry);
                    }
                }
            }
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            ShowPropertyDrives spd = propertyPage.GetParent(this) as ShowPropertyDrives;
            if (spd != null)
            {
                if (spd.MayChangeName(this, newValue))
                {
                    (this as IDrive).Name = newValue;
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Refresh ()"/>
        /// </summary>
        public override void Refresh()
        {
            subEntries = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false);
        }
        #endregion
        #region ISerializable Members
        protected AxisDrive(SerializationInfo info, StreamingContext context)
        {
            name = info.GetString("Name");
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            direction = (GeoVector)info.GetValue("Direction", typeof(GeoVector));
            movedObjectsDeserialized = info.GetValue("MovedObjects", typeof(IGeoObject[])) as IGeoObject[];
            try
            {
                dependsOn = info.GetValue("DependsOn", typeof(IDrive)) as IDrive;
            }
            catch (SerializationException)
            {
                dependsOn = null;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
            info.AddValue("Location", location);
            info.AddValue("Direction", direction);
            info.AddValue("MovedObjects", movedObjects.ToArray());
            info.AddValue("DependsOn", dependsOn);
        }
        #endregion
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            movedObjects = new ListWithEvents<IGeoObject>(movedObjectsDeserialized);
            movedObjectsDeserialized = null;
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
        }

        #endregion
        #region IJsonSerialize Members
        protected AxisDrive()// for JSON deserialisation
        {
            base.resourceIdInternal = "AxisDrive";
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("Location", location);
            data.AddProperty("Direction", direction);
            data.AddProperty("MovedObjects", movedObjects.ToArray());
            data.AddProperty("DependsOn", dependsOn);
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetProperty<string>("Name");
            location = data.GetProperty<GeoPoint>("Location");
            direction = data.GetProperty<GeoVector>("Direction");
            movedObjects = new ListWithEvents<IGeoObject>(data.GetProperty<IGeoObject[]>("MovedObjects"));
            dependsOn = data.GetProperty<AxisDrive>("DependsOn");
            data.RegisterForSerializationDoneCallback(this);
            movedObjects.OnAdded += new ListWithEvents<IGeoObject>.AddedDelegate(OnMovedObjectsAdded);
            movedObjects.OnRemoved += new ListWithEvents<IGeoObject>.RemovedDelegate(OnMovedObjectsRemoved);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            foreach (IGeoObject go in movedObjects)
            {
                go.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            }
        }
        #endregion

    }

    class ShowPropertyDrives : PropertyEntryImpl, ICommandHandler
    {
        DriveList driveList;
        public ShowPropertyDrives(DriveList driveList)
        {
            this.driveList = driveList;
            base.resourceIdInternal = "DriveList";
        }
        #region IPropertyEntry implementation
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.DriveList", false, this);
            }
        }
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>,
        /// returns the number of subentries in this property view.
        /// </summary>
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    foreach (IPropertyEntry sp in driveList)
                    {
                        res.Add(sp);
                    }
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        #endregion
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.DriveList.NewLinear":
                    {
                        string NewDriveName = StringTable.GetString("DriveList.NewLinearDriveName");
                        int MaxNr = 0;
                        foreach (IDrive drv in driveList)
                        {
                            string Name = drv.Name;
                            if (Name.StartsWith(NewDriveName))
                            {
                                try
                                {
                                    int nr = int.Parse(Name.Substring(NewDriveName.Length));
                                    if (nr > MaxNr) MaxNr = nr;
                                }
                                catch (ArgumentNullException) { } // hat garkeine Nummer
                                catch (FormatException) { } // hat was anderes als nur Ziffern
                                catch (OverflowException) { } // zu viele Ziffern
                            }
                        }
                        MaxNr += 1; // nächste freie Nummer
                        NewDriveName += MaxNr.ToString();
                        CurveDrive cd = new CurveDrive();
                        (cd as IDrive).Name = NewDriveName;
                        driveList.Add(cd);
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                        for (int i = 0; i < subEntries.Length; ++i)
                        {
                            if ((subEntries[i] as IDrive).Name == NewDriveName)
                            {
                                (subEntries[i] as IPropertyEntry).StartEdit(false);
                                break;
                            }
                        }
                        return true;
                    }
                case "MenuId.DriveList.NewAxis":
                    break;
                case "MenuId.DriveList.NewDualcurve":
                    {
                        string NewDriveName = StringTable.GetString("DriveList.NewDualcurveDriveName");
                        int MaxNr = 0;
                        foreach (IDrive drv in driveList)
                        {
                            string Name = drv.Name;
                            if (Name.StartsWith(NewDriveName))
                            {
                                try
                                {
                                    int nr = int.Parse(Name.Substring(NewDriveName.Length));
                                    if (nr > MaxNr) MaxNr = nr;
                                }
                                catch (ArgumentNullException) { } // hat garkeine Nummer
                                catch (FormatException) { } // hat was anderes als nur Ziffern
                                catch (OverflowException) { } // zu viele Ziffern
                            }
                        }
                        MaxNr += 1; // nächste freie Nummer
                        NewDriveName += MaxNr.ToString();
                        DualCurveDrive cd = new DualCurveDrive();
                        (cd as IDrive).Name = NewDriveName;
                        driveList.Add(cd);
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                        for (int i = 0; i < subEntries.Length; ++i)
                        {
                            if ((subEntries[i] as IDrive).Name == NewDriveName)
                            {
                                (subEntries[i] as IPropertyEntry).StartEdit(false);
                                break;
                            }
                        }
                        return true;
                    }
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.DriveList.NewLinear":
                    return true;
                case "MenuId.DriveList.NewAxis":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        internal void Remove(IDrive drive)
        {
            driveList.Remove(drive);
            subEntries = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(this, true);
        }
        internal bool MayChangeName(IDrive drive, string newName)
        {
            foreach (IDrive dv in driveList)
            {
                if (dv != drive)
                {
                    if (dv.Name == newName) return false;
                }
            }
            return true;
        }
    }

    internal class CurveDriveCurveProperty : PropertyEntryImpl, ICommandHandler
    {
        CurveDrive curveDrive;
        public CurveDriveCurveProperty(CurveDrive curveDrive)
        {
            this.curveDrive = curveDrive;
            base.resourceIdInternal = "CurveDrive.Curve";
        }
        #region IPropertyEntry implementation
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.AllowDrag;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.CurveDriveCurve", false, this);
            }
        }
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    if (curveDrive.MoveAlong != null)
                    {
                        res.Add((curveDrive.MoveAlong as IGeoObject).GetShowProperties(Frame));
                    }
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDriveCurve.Show":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        av.SetSelectedObject(curveDrive.MoveAlong as IGeoObject);
                    }
                    return true;
                case "MenuId.CurveDriveCurve.Set":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        if (list.Count == 1 && list[0] is ICurve)
                        {
                            curveDrive.MoveAlong = list[0].Clone() as ICurve;
                        }
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.CurveDriveCurve.Show":
                    return true;
                case "MenuId.CurveDriveCurve.Set":
                    CommandState.Enabled = false;
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        if (list.Count == 1 && list[0] is ICurve)
                        {
                            CommandState.Enabled = true;
                        }
                    }
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
    internal class AxisDriveCurveProperty : PropertyEntryImpl, ICommandHandler
    {
        AxisDrive AxisDrive;
        public AxisDriveCurveProperty(AxisDrive AxisDrive)
        {
            this.AxisDrive = AxisDrive;
            base.resourceIdInternal = "AxisDrive.Curve";
        }
        #region IPropertyEntry implementation
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.AllowDrag;
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.AxisDriveCurve", false, this);
            }
        }
        IPropertyEntry[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> res = new List<IPropertyEntry>();
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        //void OnFocusChanged(IPropertyPage sender, IPropertyEntry NewFocus, IPropertyEntry OldFocus)
        //{
        //    if (sender.FocusLeft(this, OldFocus, NewFocus))
        //    {
        //    }
        //    else if (sender.FocusEntered(this, OldFocus, NewFocus))
        //    {
        //        if (Frame.ActiveView is AnimatedView)
        //        {
        //            AnimatedView av = Frame.ActiveView as AnimatedView;
        //        }
        //    }
        //}
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.AxisDriveCurve.Show":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                    }
                    return true;
                case "MenuId.AxisDriveCurve.Set":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        subEntries = null;
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.AxisDriveCurve.Show":
                    return true;
                case "MenuId.AxisDriveCurve.Set":
                    CommandState.Enabled = false;
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        GeoObjectList list = av.GetSelectedObjects();
                        if (list.Count == 1 && list[0] is ICurve)
                        {
                            CommandState.Enabled = true;
                        }
                    }
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
}
