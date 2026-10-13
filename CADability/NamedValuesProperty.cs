using CADability.UserInterface;
using System;
using System.Collections;
using System.Runtime.Serialization;
using System.Text;

namespace CADability
{
	/// <summary>
	/// The named values of a <see cref="Project"/>: a table of user defined values (doubles, points and
	/// vectors) with a name each, which can be referred to by name in input fields and formulas. The table
	/// is saved with the project and shown as an editable group in the control center, where values are
	/// added via the context menu, renamed and removed.
	/// </summary>
	[Serializable]
	internal class NamedValuesProperty : IShowPropertyImpl, ICommandHandler, ISerializable, IJsonSerialize
	{
		private Hashtable namedValues;
		public NamedValuesProperty()
		{
			namedValues = new Hashtable();
			this.resourceIdInternal = "NamedValues";
		}
		public object GetNamedValue(string name)
		{
			return namedValues[name];
		}
		public void SetNamedValue(string name, object val)
		{
			bool editName = false;
			if (val == null) namedValues.Remove(name);
			else
			{
				if (name == null)
				{
					string prefix = null;
					if (val is double)
					{
						prefix = StringTable.GetString("NamedValues.NewDouble.Prefix");
					}
					else if (val is GeoPoint)
					{
						prefix = StringTable.GetString("NamedValues.NewGeoPoint.Prefix");
					}
					else if (val is GeoVector)
					{
						prefix = StringTable.GetString("NamedValues.NewGeoVector.Prefix");
					}
					name = GetUniqueName(prefix);
					editName = true;
				}
				namedValues[name] = val;
			}
			if (propertyTreeView != null)
			{
				subEntries = null;
				propertyTreeView.OpenSubEntries(this, false);
				propertyTreeView.OpenSubEntries(this, true);
				if (editName)
				{
					Frame.SetControlCenterFocus("Project", null, false, false);
					IPropertyEntry sp = FindEntry(name);
					if (sp != null)
					{
						propertyTreeView.StartEditLabel(sp);
					}
				}
			}
		}

		public string GetCode()
		{
			StringBuilder sb = new StringBuilder();
			foreach (DictionaryEntry de in namedValues)
			{
				// GeoPoint p1 { get { return (GeoPoint)namedValues["p1"]; } }
				string pattern = @"%type% %name% { get { return (%type%)namedValues[""%name%""]; } }
";
				pattern = pattern.Replace("%type%", de.Value.GetType().Name);
				pattern = pattern.Replace("%name%", de.Key as string);
				sb.Append(pattern);
			}
			return sb.ToString();
		}
		public Hashtable Table { get { return namedValues; } }
		#region IShowPropertyImpl Overrides
		private IShowProperty[] subEntries;
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
		public override ShowPropertyLabelFlags LabelType
		{
			get
			{
				return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu;
			}
		}
		public override MenuWithHandler[] ContextMenu
		{
			get
			{
				return MenuResource.LoadMenuDefinition("MenuId.NamedValues", false, this);
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
		private class LabelTextComparer : IComparer
		{
			public LabelTextComparer() { }
			#region IComparer Members

			public int Compare(object x, object y)
			{
				IPropertyEntry sp1 = x as IPropertyEntry;
				IPropertyEntry sp2 = y as IPropertyEntry;
				return sp1.Label.CompareTo(sp2.Label);
			}

			#endregion
		}
		public override IShowProperty[] SubEntries
		{
			get
			{
				if (subEntries == null)
				{
					ArrayList res = new ArrayList();

					foreach (DictionaryEntry de in namedValues)
					{
						string currentName = de.Key as string;
						if (de.Value is double)
						{
							LengthProperty lp = new LengthProperty(Frame, null);
							lp.UserData.Add("Name", de.Key);
							lp.OnGetValue = () => (double)namedValues[currentName];
							lp.OnSetValue = l => namedValues[currentName] = l;
							lp.LabelTextChanged = OnLengthLabelChanged;
							lp.LabelText = de.Key as string;
							lp.LabelIsEditable = true;
							res.Add(lp);
						}
						else if (de.Value is GeoPoint)
						{
							GeoPointProperty gpp = new GeoPointProperty(Frame, "");
							gpp.UserData.Add("Name", de.Key);
							gpp.OnGetValue = () => (GeoPoint)namedValues[currentName];
							gpp.OnSetValue = p => namedValues[currentName] = p;
							gpp.LabelTextChanged = OnGeoPointLabelChanged;
							gpp.LabelText = de.Key as string;
							gpp.LabelIsEditable = true;
							res.Add(gpp);
						}
						else if (de.Value is GeoVector)
						{
							GeoVectorProperty gvp = new GeoVectorProperty(Frame, "");
							gvp.UserData.Add("Name", de.Key);
							gvp.IsNormedVector = false;
							gvp.IsAngle = false;
							gvp.OnGetValue = () => (GeoVector)namedValues[currentName];
							gvp.OnSetValue = v => namedValues[currentName] = v;
							gvp.LabelTextChanged = OnGeoVectorLabelChanged;
							gvp.LabelText = de.Key as string;
							gvp.LabelIsEditable = true;
							res.Add(gvp);
						}
					}
					res.Sort(new LabelTextComparer());
					subEntries = res.ToArray(typeof(IShowProperty)) as IShowProperty[];
				}
				return subEntries;
			}
		}
		#endregion

		#region ICommandHandler Members
		private IPropertyEntry FindEntry(string name)
		{
			for (int i = 0; i < SubEntries.Length; ++i)
			{
				if ((subEntries[i] as IPropertyEntry).Label == name)
					return subEntries[i] as IPropertyEntry;
			}
			return null;
		}
		private void Remove(string name)
		{
			if (namedValues.ContainsKey(name))
			{
				namedValues.Remove(name);
				if (propertyTreeView != null)
				{
					subEntries = null;
					propertyTreeView.Refresh(this);
					propertyTreeView.OpenSubEntries(this, true);
				}
			}
		}
		virtual public bool OnCommand(string MenuId)
		{
			switch (MenuId)
			{
				case "MenuId.NamedValues.NewGeoPoint":
					InsertGeoPoint();
					return true;
				case "MenuId.NamedValues.NewGeoVector":
					InsertGeoVector();
					return true;
				case "MenuId.NamedValues.NewDouble":
					InsertDouble();
					return true;
				default:
					if (MenuId.StartsWith("MenuId.NamedValue.Remove"))
					{
						string name = MenuId.Remove(0, "MenuId.NamedValue.Remove.".Length);
						Remove(name);
						return true;
					}
					if (MenuId.StartsWith("MenuId.NamedValue.EditName"))
					{
						string name = MenuId.Remove(0, "MenuId.NamedValue.EditName.".Length);
						IPropertyEntry sp = FindEntry(name);
						if (sp != null && propertyTreeView != null)
						{
							propertyTreeView.StartEditLabel(sp);
						}
						return true;
					}
					break;
			}
			return false;
		}
		private string GetUniqueName(string prefix)
		{
			int i = 1;
			while (namedValues.ContainsKey(prefix + i.ToString())) ++i;
			return prefix + i.ToString();
		}
		private void ReflectNewEntry(string name)
		{
			subEntries = null;
			if (propertyTreeView != null)
			{
				propertyTreeView.Refresh(this);
				propertyTreeView.OpenSubEntries(this, true); // damit extistiert auch subEntries
				for (int i = 0; i < SubEntries.Length; ++i)
				{
					if (subEntries[i].LabelText == name)
					{
						propertyTreeView.SelectEntry(subEntries[i] as IPropertyEntry);
						break;
					}
				}
			}
		}
		private void InsertGeoPoint()
		{
			string prefix = StringTable.GetString("NamedValues.NewGeoPoint.Prefix");
			string Name = GetUniqueName(prefix);
			namedValues[Name] = new GeoPoint(0.0, 0.0, 0.0);
			ReflectNewEntry(Name);
		}
		private void InsertGeoVector()
		{
			string prefix = StringTable.GetString("NamedValues.NewGeoVector.Prefix");
			string Name = GetUniqueName(prefix);
			namedValues[Name] = new GeoVector(0.0, 0.0, 0.0);
			ReflectNewEntry(Name);
		}
		private void InsertDouble()
		{
			string prefix = StringTable.GetString("NamedValues.NewDouble.Prefix");
			string Name = GetUniqueName(prefix);
			namedValues[Name] = 0.0;
			ReflectNewEntry(Name);
		}
		virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			// TODO:  Add NamedValuesProperty.OnUpdateCommand implementation
			return false;
		}
		void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

		#endregion

		private void Rename(string oldName, string newName)
		{
			if (namedValues.ContainsKey(newName))
			{
				namedValues.Remove(newName);
			}
			object val = namedValues[oldName];
			namedValues.Remove(oldName);
			namedValues[newName] = val;
			// immer Refresh, damit die Menues mit den neuen Namen aufgebaut werden
			if (propertyTreeView != null && subEntries != null)
			{
				subEntries = null; // löschen
				propertyTreeView.Refresh(this);
				propertyTreeView.OpenSubEntries(this, false);
				propertyTreeView.OpenSubEntries(this, true);
				for (int i = 0; i < SubEntries.Length; ++i)
				{
					if (subEntries[i].LabelText == newName)
					{
						propertyTreeView.SelectEntry(subEntries[i] as IPropertyEntry);
						break;
					}
				}
			}
		}

		private void OnGeoVectorLabelChanged(EditableProperty<GeoVector> sender, string newLabel)
		{
			OnLabelChanged(sender.UserData.GetData("Name") as string, newLabel);
		}
		private void OnGeoPointLabelChanged(EditableProperty<GeoPoint> sender, string newLabel)
		{
			OnLabelChanged(sender.UserData.GetData("Name") as string, newLabel);
		}
		private void OnLengthLabelChanged(EditableProperty<double> sender, string newLabel)
		{
			OnLabelChanged(sender.UserData.GetData("Name") as string, newLabel);
		}
		private void OnLabelChanged(string oldName, string newLabel)
		{
			if (oldName == newLabel) return;
			Rename(oldName, newLabel);
		}
		#region ISerializable Members
		/// <summary>
		/// Constructor required by deserialization
		/// </summary>
		/// <param name="info">SerializationInfo</param>
		/// <param name="context">StreamingContext</param>
		protected NamedValuesProperty(SerializationInfo info, StreamingContext context)
			: this()
		{
			namedValues = info.GetValue("NamedValues", typeof(Hashtable)) as Hashtable;
		}
		/// <summary>
		/// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
		/// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
		/// </summary>
		/// <param name="info">receives the data</param>
		/// <param name="context">the destination of the serialization</param>
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("NamedValues", namedValues);
		}

		#endregion
		#region IJsonSerialize Members
		/// <summary>
		/// Writes the named values for the JSON serialization (<see cref="IJsonSerialize"/>).
		/// </summary>
		/// <param name="data">receives the data</param>
		public void GetObjectData(IJsonWriteData data)
		{
			data.AddProperty("NamedValues", namedValues);
		}
		/// <summary>
		/// Restores the named values from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/>
		/// (<see cref="IJsonSerialize"/>). Data without named values gives an empty table.
		/// </summary>
		/// <param name="data">the data to read from</param>
		public void SetObjectData(IJsonReadData data)
		{
			namedValues = data.GetPropertyOrDefault<Hashtable>("NamedValues") ?? new Hashtable();
		}
		#endregion
	}
}
