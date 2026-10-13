using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry that groups the attribute properties of an <see cref="IGeoObject"/> (its colour via a
    /// <see cref="ColorSelectionProperty"/>, when the object implements <see cref="IColorDef"/>, and its layer via a
    /// <see cref="LayerSelectionProperty"/>) under one "Attributes" title. It is used by the construction actions to show
    /// the attributes of the object being constructed in the property grid.
    /// </summary>

    public class AttributesProperty : IShowPropertyImpl
    {
        private ColorSelectionProperty colorSelectProp;
        private LayerSelectionProperty layerSelectProp;
        public AttributesProperty(IGeoObject o, IFrame frame)
        {
            IColorDef clr = o as IColorDef;
            if (clr != null)
            {
                colorSelectProp = new ColorSelectionProperty(o, "ColorDef", "GeoObject.Color", frame.Project.ColorList, ColorList.StaticFlags.allowAll);
            }
            layerSelectProp = new LayerSelectionProperty(o, StringTable.GetString("GeoObject.Layer"), frame.Project.LayerList);
            resourceIdInternal = "GeoObject.Attributes";
        }
        #region IShowPropertyImpl Overrides
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
                if (colorSelectProp != null)
                    return 2;
                else
                    return 1;
            }
        }
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
        {
            get
            {
                IShowProperty[] res;
                if (colorSelectProp != null)
                {
                    res = new IShowProperty[2];
                    res[0] = colorSelectProp as IShowProperty;
                    res[1] = layerSelectProp as IShowProperty;
                }
                else
                {
                    res = new IShowProperty[1];
                    res[0] = layerSelectProp as IShowProperty;
                }
                return res;
            }
        }

        #endregion
    }
}
