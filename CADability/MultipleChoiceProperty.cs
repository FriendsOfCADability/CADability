using System;

namespace CADability.UserInterface
{

    /* derived:
    public class LinePatternSelectionProperty : MultipleChoiceProperty
    public class LayerSelectionProperty : MultipleChoiceProperty
    public class ColorSelectionProperty : MultipleChoiceProperty
    public class BooleanProperty : MultipleChoiceProperty, ISerializable, IDeserializationCallback, ISettingChanged, IJsonSerialize, IJsonSerializeDone
    public class StyleSelectionProperty : MultipleChoiceProperty
    public class DimensionStyleSelectionProperty : MultipleChoiceProperty
    public class LineWidthSelectionProperty : MultipleChoiceProperty
    public class HatchStyleSelectionProperty : MultipleChoiceProperty
    public class MultipleChoiceSetting : MultipleChoiceProperty, ISerializable, ISettingChanged
     * */

    /// <summary>
    /// Property page entry which shows one text out of a fixed set of choices and lets the user pick another one from a
    /// drop down list. It is the base class of the attribute selection entries (<see cref="ColorSelectionProperty"/>,
    /// <see cref="CADability.Attribute.LayerSelectionProperty"/>, <see cref="LinePatternSelectionProperty"/> etc.), of
    /// <see cref="BooleanProperty"/> and of <see cref="MultipleChoiceSetting"/>. A selection by the user is reported
    /// through <see cref="SelectionChangedEvent"/> and <see cref="ValueChangedEvent"/>; derived classes override
    /// <see cref="OnSelectionChanged(string)"/> to apply it to the object they represent.
    /// </summary>
    public class MultipleChoiceProperty : PropertyEntryImpl
    {
        /// <summary>
        /// The text of the label (left side of the entry), set by some derived classes from their resource id. The label
        /// displayed by the property page is taken from the resource id, not from this field.
        /// </summary>
        protected string propertyLabelText; // der Text auf der linken Seite
        internal string[] choices; // die Auswahlmöglichkeiten
        //protected int[] imageIndices; // die Indizes in der ImageList, selbe Reihenfolge wie choices
        //protected ImageList images; // die ImageList (kann fehlen)
        /// <summary>
        /// The text shown (in gray) when no choice is selected, see <see cref="SetUnselectedText(string)"/>. null: the value
        /// is shown empty.
        /// </summary>
        protected string unselectedText; // der Text, wenn nichts ausgewählt ist (wenn null, dann ganz leer)
        /// <summary>
        /// Back reference to any user item. Not used by the MultipleChoiceProperty object itself.
        /// </summary>
        /// <value>
        /// An arbitrary object supplied by the user of this property entry, or null
        /// </value>
        public object user;

        /// <summary>
        /// Constructor with initialization.
        /// </summary>
        /// <param name="resourceId">Resource id of the text displayed on the left side</param>
        /// <param name="Choices">The choices</param>
        /// <param name="InitialSelection">The initially selected text (one of the choices)</param>
        public MultipleChoiceProperty(string resourceId, string[] Choices, string InitialSelection)
            : this()
        {
            base.resourceIdInternal = resourceId;
            choices = Choices;
            SelectedText = InitialSelection;
        }
        /// <summary>
        /// Creates the entry with the choices taken from the string table: the entry <paramref name="resourceId"/> + ".Values"
        /// must contain all choices separated by its first character (see <see cref="StringTable.GetSplittedStrings(string)"/>).
        /// </summary>
        /// <param name="resourceId">Resource id of the label; resourceId + ".Values" provides the choices</param>
        /// <param name="InitialSelection">Index of the initially selected choice; 0 is used when it is out of range</param>
        public MultipleChoiceProperty(string resourceId, int InitialSelection)
            : this()
        {
            base.resourceIdInternal = resourceId;
            choices = StringTable.GetSplittedStrings(resourceId + ".Values");
            if (InitialSelection >= choices.Length) InitialSelection = 0;
            SelectedText = choices[InitialSelection];
        }
        /// <summary>
        /// Empty constructor for the use by derived classes. The derived class has to set at least <c>propertyLabelText</c>
        /// and <c>choices</c>.
        /// </summary>
        public MultipleChoiceProperty()
        {
        }
        //private int ImageIndex(int Index)
        //{
        //    if (imageIndices != null) return imageIndices[Index];
        //    return -1;
        //}
        //private void fillPopupList()
        //{
        //    if (popupList != null)
        //    {
        //        popupList.RemoveAllItems();
        //        for (int i = 0; i < choices.Length; ++i)
        //        {
        //            popupList.AddItem(choices[i], ImageIndex(i), choices[i]);
        //            if (selectedText == choices[i]) popupList.SelectedIndex = i;
        //        }
        //    }
        //}
        /// <summary>
        /// The texts the user can choose from, in the order of the drop down list. Getting and setting work on a copy of the
        /// array; setting does not change the current selection.
        /// </summary>
        public string[] Choices
        {
            get
            {
                return choices.Clone() as string[];
            }
            set
            {
                choices = value.Clone() as string[];
                //fillPopupList();
            }
        }

        /// <summary>
        /// The currently selected choice, or null when nothing is selected.
        /// </summary>
        public string SelectedText { get; protected set; }
        /// <summary>
        /// Returns the index of the given text in <see cref="Choices"/>.
        /// </summary>
        /// <param name="choice">The text to look for</param>
        /// <returns>The index, or -1 when the text is not one of the choices</returns>
        public int ChoiceIndex(string choice)
        {
            for (int i = 0; i < choices.Length; ++i)
            {
                if (choices[i] == choice) return i;
            }
            return -1;
        }
        /// <summary>
        /// Sets the text which is shown (in gray) when nothing is selected, e.g. "multiple values" when the entry represents
        /// several objects with different attributes.
        /// </summary>
        /// <param name="ResourceID">Resource id of the text in the string table</param>
        public void SetUnselectedText(string ResourceID)
        {
            unselectedText = StringTable.GetString(ResourceID);
        }
        /// <summary>
        /// Delegate for <see cref="SelectionChangedEvent"/>.
        /// </summary>
        /// <param name="sender">The entry whose selection changed; query <see cref="SelectedText"/> or <see cref="CurrentIndex"/> for the selection</param>
        /// <param name="arg">Always <see cref="EventArgs.Empty"/></param>
        public delegate void SelectionChangedDelegate(MultipleChoiceProperty sender, EventArgs arg);
        /// <summary>
        /// Raised when the user picks a choice from the drop down list (or a derived class reports a new selection).
        /// </summary>
        public event SelectionChangedDelegate SelectionChangedEvent;
        /// <summary>
        /// Raised after <see cref="SelectionChangedEvent"/>, with the newly selected text as the value.
        /// </summary>
        public event ValueChangedDelegate ValueChangedEvent;
        /// <summary>
        /// Sets the current selection by index without raising the events. Used to synchronize the entry with the object it
        /// represents.
        /// </summary>
        /// <param name="toSelect">Index into <see cref="Choices"/>; a negative or invalid index clears the selection</param>
        public virtual void SetSelection(int toSelect)
        {
            if (toSelect < 0)
            {
                SelectedText = null;
            }
            else
            {
                try
                {
                    SelectedText = choices[toSelect];
                }
                catch (IndexOutOfRangeException)
                {
                    SelectedText = null;
                }
            }
        }
        /// <summary>
        /// Called when the user has picked a choice. Raises <see cref="SelectionChangedEvent"/>, stores the text in
        /// <see cref="SelectedText"/> and raises <see cref="ValueChangedEvent"/>. Derived classes override it to pass the
        /// selection on to the object they represent.
        /// </summary>
        /// <param name="selected">The selected text</param>
        protected virtual void OnSelectionChanged(string selected)
        {
            if (SelectionChangedEvent != null) SelectionChangedEvent(this, EventArgs.Empty);
            SelectedText = selected;
            if (ValueChangedEvent != null) ValueChangedEvent(this, SelectedText);
        }
        /// <summary>
        /// The index of <see cref="SelectedText"/> in <see cref="Choices"/>, or -1 when nothing is selected.
        /// </summary>
        public int CurrentIndex
        {
            get
            {
                for (int i = 0; i < choices.Length; i++)
                    if (SelectedText == choices[i])
                        return i;
                return -1;
            }
        }
        #region IPropertyEntry Members
        /// <inheritdoc/>
        public override PropertyEntryType Flags 
        {
            get
            {
                return PropertyEntryType.Selectable | PropertyEntryType.DropDown;
            }
        }
        /// <inheritdoc/>
        public override string[] GetDropDownList()
        {
            return Choices;
        }
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                if (SelectedText != null) return SelectedText;
                if (unselectedText != null) return "[[ColorTxt:128:128:128]]" + unselectedText;
                return "";
            }
        }
        /// <inheritdoc/>
        public override void ListBoxSelected(int selectedIndex)
        {
            if (selectedIndex >= 0)
            {
                SelectedText = Choices[selectedIndex];
                OnSelectionChanged(SelectedText);
                // the following is already done by OnSelectionChanged
                //SelectionChangedEvent?.Invoke(this, EventArgs.Empty);
                //ValueChangedEvent?.Invoke(this, selectedText);
            }
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        #endregion
    }
}
