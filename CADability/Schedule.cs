using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{

    public interface ISchedule
    {
        void SetDrivePositions(double time, AnimatedView animatedView);
    }

    [Serializable]
    public class ScheduleList : IEnumerable<Schedule>, ISerializable, IDeserializationCallback, IJsonSerialize
    {
        private List<Schedule> schedules;
        Schedule[] deserialized;
        public ScheduleList()
        {
            schedules = new List<Schedule>();
        }
        public void Add(Schedule toAdd)
        {   // Namensgleichheit überprüfen
            schedules.Add(toAdd);
        }
        public void Remove(Schedule schedule)
        {
            schedules.Remove(schedule);
        }
        #region IEnumerable<Schedule> Members
        IEnumerator<Schedule> IEnumerable<Schedule>.GetEnumerator()
        {
            return schedules.GetEnumerator();
        }
        #endregion
        #region IEnumerable Members
        IEnumerator IEnumerable.GetEnumerator()
        {
            return schedules.GetEnumerator();
        }
        #endregion
        #region ISerializable Members
        protected ScheduleList(SerializationInfo info, StreamingContext context)
        {
            deserialized = info.GetValue("Schedules", typeof(Schedule[])) as Schedule[];
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Schedules", schedules.ToArray());
        }
        #endregion
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            schedules = new List<Schedule>(deserialized);
            deserialized = null;
        }

        #endregion
        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Schedules", schedules.ToArray());
        }

        public void SetObjectData(IJsonReadData data)
        {
            schedules = new List<Schedule>(data.GetProperty<Schedule[]>("Schedules"));
        }

        #endregion

        public Schedule Find(string name)
        {
            for (int i = 0; i < schedules.Count; ++i)
            {
                if (schedules[i].Name == name) return schedules[i];
            }
            return null;
        }
        public Schedule this[int Index]
        {
            get { return schedules[Index]; }
            set { schedules[Index] = value; }
        }
        public int Count
        {
            get
            {
                return schedules.Count;
            }
        }
    }

    [Serializable]
    public class Schedule : IShowPropertyImpl, ISchedule, ICommandHandler, ISerializable, IDeserializationCallback, IJsonSerialize
    {
        // Antrieb, Zeitpunkt, Position
        Dictionary<IDrive, SortedList<double, double>> steps;
        DriveList driveList;
        string name;
        object[] deserialized; // nur zum deserialisieren
        public Schedule(DriveList driveList)
        {
            this.driveList = driveList;
            steps = new Dictionary<IDrive, SortedList<double, double>>();
            base.resourceIdInternal = "Schedule";
        }
        public void AddPosition(IDrive drive, double time, double position)
        {
            if (drive == null) return;
            SortedList<double, double> addTo;
            if (!steps.TryGetValue(drive, out addTo))
            {
                addTo = new SortedList<double, double>();
                steps[drive] = addTo;
            }
            addTo[time] = position;
            if (propertyTreeView != null)
            {
                subEntries = null;
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        internal void DriveChanged(IDrive oldDrive, IDrive newDrive, double time, double position)
        {
            if (oldDrive != null)
            {
                SortedList<double, double> toModify;
                if (steps.TryGetValue(oldDrive, out toModify))
                {
                    toModify.Remove(time);
                }
            }
            if (newDrive != null) AddPosition(newDrive, time, position);
        }
        internal void TimeChanged(IDrive drive, double oldTime, double newTime, double position)
        {
            if (drive != null)
            {
                SortedList<double, double> toModify;
                if (steps.TryGetValue(drive, out toModify))
                {
                    toModify.Remove(oldTime);
                }
                AddPosition(drive, newTime, position);
            }
        }
        internal void PositionChanged(IDrive drive, double time, double oldPosition, double newPosition)
        {
            if (drive != null)
            {
                SortedList<double, double> toModify;
                if (steps.TryGetValue(drive, out toModify))
                {
                    toModify[time] = newPosition;
                }
            }
        }
        public void Remove(IDrive drive)
        {
            steps.Remove(drive);
        }
        public void Remove(IDrive drive, double time)
        {
            if (drive != null)
            {
                SortedList<double, double> toModify;
                if (steps.TryGetValue(drive, out toModify))
                {
                    toModify.Remove(time);
                    if (propertyTreeView != null)
                    {
                        subEntries = null;
                        propertyTreeView.Refresh(this);
                        propertyTreeView.OpenSubEntries(this, true);
                    }
                }
            }
        }
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
        #region ISchedule Members

        void ISchedule.SetDrivePositions(double time, AnimatedView animatedView)
        {
            foreach (KeyValuePair<IDrive, SortedList<double, double>> kv in steps)
            {
                double starttime = time;
                double endtime = time;
                double startpos = 0.0;
                double endpos = 0.0;
                foreach (KeyValuePair<double, double> tp in kv.Value)
                {
                    if (tp.Key < time)
                    {
                        endpos = startpos = tp.Value;
                        endtime = starttime = tp.Key;
                    }
                    if (tp.Key >= time)
                    {
                        endpos = tp.Value;
                        endtime = tp.Key;
                        break;
                    }
                }
                double f; // linear interpolieren
                if (endtime == starttime) f = 0.0;
                else f = (time - starttime) / (endtime - starttime);
                double pos = startpos + f * (endpos - startpos);
                kv.Key.Position = pos;
                // System.Diagnostics.Trace.WriteLine(kv.Key.Name + ": " + time.ToString() + ", " + pos.ToString());
            }
        }

        #endregion
        #region IShowProperty implementation
        /// <summary>
        /// Implements <see cref="IShowProperty.LabelText"/>.
        /// </summary>
        public override string LabelText
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Editable | ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
                if (propertyTreeView != null)
                {
                    ShowPropertySchedules sps = propertyTreeView.GetParent(this) as ShowPropertySchedules;
                    if (sps != null)
                    {
                        if (sps.ActiveSchedule == this) res |= ShowPropertyLabelFlags.Bold;
                    }
                }
                return res;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.LabelChanged (string)"/>
        /// </summary>
        /// <param name="NewText"></param>
        public override void LabelChanged(string NewText)
        {
            try
            {
                Name = NewText;
                if (propertyTreeView != null) propertyTreeView.Refresh(this);
            }
            catch (NameAlreadyExistsException) { }
        }
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition("MenuId.Schedule", false, this);
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
                {
                    List<IShowProperty> res = new List<IShowProperty>();
                    foreach (KeyValuePair<IDrive, SortedList<double, double>> kv in steps)
                    {
                        foreach (KeyValuePair<double, double> tp in kv.Value)
                        {
                            res.Add(new PositionProperty(this, driveList, kv.Key, tp.Key, tp.Value));
                        }
                    }
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {
            if (subEntries != null)
            {
                steps.Clear(); // alle rauswerfen und neu aufbauen
                for (int i = 0; i < subEntries.Length; ++i)
                {
                    PositionProperty pp = subEntries[i] as PositionProperty;
                    if (pp != null)
                    {
                        if (pp.Drive == null) continue;
                        SortedList<double, double> addTo;
                        if (!steps.TryGetValue(pp.Drive, out addTo))
                        {
                            addTo = new SortedList<double, double>();
                            steps[pp.Drive] = addTo;
                        }
                        addTo[pp.Time] = pp.Position;
                    }
                }
            }
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Schedule.NewPosition":
                    propertyTreeView.OpenSubEntries(this, true); // damit gibts die subentries
                    List<IShowProperty> tmp = new List<IShowProperty>(subEntries);
                    PositionProperty pp = new PositionProperty(this, driveList);
                    tmp.Add(pp);
                    subEntries = tmp.ToArray();
                    propertyTreeView.Refresh(this);
                    propertyTreeView.OpenSubEntries(pp, true);
                    propertyTreeView.SelectEntry(pp); // besser wäre den ersten Eintrag davon
                    return true;
                case "MenuId.Schedule.Sort":
                    propertyTreeView.SelectEntry(this); // damit der Focus hierhin geht und die Änderungen übernommen werden
                    subEntries = null;
                    propertyTreeView.Refresh(this);
                    return true;
                case "MenuId.Schedule.MakeCurrent":
                    {
                        ShowPropertySchedules sps = propertyTreeView.GetParent(this) as ShowPropertySchedules;
                        if (sps != null)
                        {
                            sps.ActiveSchedule = this;
                        }
                    }
                    return true;
                case "MenuId.Schedule.Remove":
                    {
                        ShowPropertySchedules sps = propertyTreeView.GetParent(this) as ShowPropertySchedules;
                        if (sps != null)
                        {
                            sps.Remove(this);
                        }
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Schedule.NewPosition":
                    return true;
                case "MenuId.Schedule.Sort":
                    return true;
                case "MenuId.Schedule.MakeCurrent":
                    return true;
                case "MenuId.Schedule.Remove":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region ISerializable Members
        protected Schedule(SerializationInfo info, StreamingContext context)
        {
            base.resourceIdInternal = "Schedule";
            name = info.GetString("Name");
            driveList = info.GetValue("DriveList", typeof(DriveList)) as DriveList;
            deserialized = info.GetValue("Steps", typeof(object[])) as object[];
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {   // Templates kann man zwar serialisieren, aber beim deserialisieren erwarten sie eine bestimmte Version
            // und das kann man nicht erfüllen. Deshalb werden arrays gemacht
            info.AddValue("Name", name);
            info.AddValue("DriveList", driveList);
            List<object> l = new List<object>();
            foreach (KeyValuePair<IDrive, SortedList<double, double>> kv in steps)
            {
                foreach (KeyValuePair<double, double> tp in kv.Value)
                {
                    l.Add(kv.Key);
                    l.Add(tp.Key);
                    l.Add(tp.Value);
                }
            }
            info.AddValue("Steps", l.ToArray());
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            steps = new Dictionary<IDrive, SortedList<double, double>>();
            for (int i = 0; i < deserialized.Length; i = i + 3)
            {
                AddPosition(deserialized[i] as IDrive, (double)deserialized[i + 1], (double)deserialized[i + 2]);
            }
            deserialized = null;
        }
        #endregion
        #region IJsonSerialize Members
        protected Schedule()
        {
            base.resourceIdInternal = "Schedule";
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("DriveList", driveList);
            List<object> l = new List<object>();
            foreach (KeyValuePair<IDrive, SortedList<double, double>> kv in steps)
            {
                foreach (KeyValuePair<double, double> tp in kv.Value)
                {
                    l.Add(kv.Key);
                    l.Add(tp.Key);
                    l.Add(tp.Value);
                }
            }
            data.AddProperty("Steps", l.ToArray());
        }

        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetProperty<string>("Name");
            driveList = data.GetProperty<DriveList>("DriveList");
            object[] l = data.GetProperty<object[]>("Steps");
            steps = new Dictionary<IDrive, SortedList<double, double>>();
            for (int i = 0; i < l.Length; i = i + 3)
            {
                if (l[i + 1] is string)
                {   // why is this a string and not a double?
                    AddPosition(l[i] as IDrive, double.Parse((string)l[i + 1]), double.Parse((string)l[i + 2]));
                }
                else
                {
                    AddPosition(l[i] as IDrive, (double)l[i + 1], (double)l[i + 2]);
                }
            }
        }

        #endregion

    }

    internal class PositionProperty : IShowPropertyImpl, ICommandHandler
    {
        IDrive drive;
        double time;
        IDrive orgDrive; // Anfangsdaten, wenn!=null, dann wird geändert, sonst neu gemacht
        double orgTime;
        double orgPosition;
        double position;
        Schedule schedule;
        DriveList driveList;
        public PositionProperty(Schedule schedule, DriveList driveList)
        {
            this.driveList = driveList;
            this.schedule = schedule;
        }
        public PositionProperty(Schedule schedule, DriveList driveList, IDrive drive, double time, double position)
        {
            this.driveList = driveList;
            this.schedule = schedule;
            this.drive = drive;
            this.position = position;
            this.time = time;
            orgDrive = drive;
            orgTime = time;
            orgPosition = position;
        }
        public double Time
        {
            get
            {
                return time;
            }
            set
            {
                schedule.TimeChanged(drive, time, value, position);
                time = value;
            }
        }
        public double Position
        {
            get
            {
                return position;
            }
            set
            {
                schedule.PositionChanged(drive, time, position, value);
                position = value;
            }
        }
        public IDrive Drive
        {
            get
            {
                return drive;
            }
        }
        #region IShowProperty implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
                return res;
            }
        }
        public override string LabelText
        {
            get
            {
                if (drive != null) return drive.Name + ": " + time.ToString();
                return StringTable.GetString("Drive.Position.Undefined");
            }
            set
            {
                base.LabelText = value;
            }
        }
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition("MenuId.DrivePosition", false, this);
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
                {
                    List<IShowProperty> res = new List<IShowProperty>();
                    List<string> selections = new List<string>();
                    foreach (IDrive dv in driveList)
                    {
                        selections.Add(dv.Name);
                    }
                    string initial = "";
                    if (drive != null) initial = drive.Name;
                    MultipleChoiceProperty mcp = new MultipleChoiceProperty("Position.Drive", selections.ToArray(), initial);
                    mcp.ValueChangedEvent += new ValueChangedDelegate(OnDriveChanged);
                    res.Add(mcp);
                    res.Add(new DoubleProperty(this, "Time", "Drive.Time", this.Frame));
                    res.Add(new DoubleProperty(this, "Position", "Drive.Position", this.Frame));
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnDriveChanged(object sender, object NewValue)
        {
            IDrive newDrive = driveList.Find(NewValue as string);
            schedule.DriveChanged(drive, newDrive, time, position);
            drive = newDrive;
        }
        void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {
            //if (sender.FocusLeft(this, OldFocus, NewFocus))
            //{
            //    if (orgDrive != null)
            //    {
            //        if (orgDrive != drive || orgTime != time || orgPosition != position)
            //        {
            //            if (orgDrive == drive && orgTime == time)
            //            {
            //                schedule.PositionChanged(drive, time, orgPosition, position);
            //            }
            //            else
            //            {
            //                schedule.Remove(orgDrive, orgTime);
            //                schedule.AddPosition(drive, time, position);
            //            }
            //        }
            //    }
            //    else
            //    {
            //        schedule.AddPosition(drive, time, position);
            //    }
            //}
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.DrivePosition.Remove":
                    schedule.Remove(drive, time);
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.DrivePosition.Remove":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }

    class ShowPropertySchedules : IShowPropertyImpl, ICommandHandler
    {
        ScheduleList scheduleList;
        DriveList driveList;
        Schedule active; // mit dem wird simuliert
        public ShowPropertySchedules(Model model)
        {
            scheduleList = model.AllSchedules;
            driveList = model.AllDrives;
            base.resourceIdInternal = "Schedule.List";
            if (scheduleList.Count > 0)
            {
                active = scheduleList[0];
            }
        }
        public Schedule ActiveSchedule
        {
            get { return active; }
            set
            {
                active = value;
                if (subEntries != null && propertyTreeView != null)
                {
                    for (int i = 0; i < subEntries.Length; ++i)
                    {
                        propertyTreeView.Refresh(subEntries[i] as IPropertyEntry);
                    }
                }
            }
        }
        #region IShowProperty implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
                return res;
            }
        }
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition("MenuId.ScheduleList", false, this);
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
                {
                    List<IShowProperty> res = new List<IShowProperty>();
                    foreach (IShowProperty sp in scheduleList)
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
                case "MenuId.ScheduleList.NewSchedule":
                    Schedule sc = new Schedule(driveList);
                    string NewScheduleName = StringTable.GetString("ScheduleList.NewScheduleName");
                    int MaxNr = 0;
                    foreach (Schedule drv in scheduleList)
                    {
                        string Name = drv.Name;
                        if (Name.StartsWith(NewScheduleName))
                        {
                            try
                            {
                                int nr = int.Parse(Name.Substring(NewScheduleName.Length));
                                if (nr > MaxNr) MaxNr = nr;
                            }
                            catch (ArgumentNullException) { } // hat garkeine Nummer
                            catch (FormatException) { } // hat was anderes als nur Ziffern
                            catch (OverflowException) { } // zu viele Ziffern
                        }
                    }
                    MaxNr += 1; // nächste freie Nummer
                    NewScheduleName += MaxNr.ToString();
                    sc.Name = NewScheduleName;
                    if (active == null) active = sc;
                    scheduleList.Add(sc);
                    subEntries = null;
                    propertyTreeView.Refresh(this);
                    propertyTreeView.OpenSubEntries(this, true);
                    for (int i = 0; i < subEntries.Length; ++i)
                    {
                        if ((subEntries[i] as Schedule).Name == NewScheduleName)
                        {
                            propertyTreeView.StartEditLabel(subEntries[i] as IPropertyEntry);
                            break;
                        }
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.ScheduleList.NewSchedule":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        internal void Remove(Schedule schedule)
        {
            scheduleList.Remove(schedule);
            if (schedule == active)
            {
                active = null;
                if (scheduleList.Count > 0) active = scheduleList[0];
            }
            subEntries = null;
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        internal bool MayChangeName(Schedule schedule, string newName)
        {
            foreach (Schedule sc in scheduleList)
            {
                if (sc != schedule)
                {
                    if (sc.Name == newName) return false;
                }
            }
            return true;
        }
    }

}
