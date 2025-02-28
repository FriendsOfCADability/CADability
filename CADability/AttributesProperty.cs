using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Verbindet die Eigenschaften Layer, ColorDef, Style für die Aktionen
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
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
