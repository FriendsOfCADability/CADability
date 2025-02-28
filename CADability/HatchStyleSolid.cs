using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// A HatchStyle which fills the shape with a solid color.
    /// May be used as a base class for different kinds of solid fillings.
    /// </summary>
    [Serializable()]
    public class HatchStyleSolid : HatchStyle, ISerializable
    {
        private ColorDef color;
        /// <summary>
        /// Empty construction
        /// </summary>
        public HatchStyleSolid()
        {
            // 
            // TODO: Add constructor logic here
            //
            resourceIdInternal = "HatchStyleNameSolid";
        }
        public ColorDef Color
        {
            get { return color; }
            set { color = value; }
        }
        public override GeoObjectList GenerateContent(CompoundShape shape, Plane plane)
        {
            GeoObjectList res = new GeoObjectList();
            for (int i = 0; i < shape.SimpleShapes.Length; ++i)
            {
                SimpleShape ss = shape.SimpleShapes[i].Clone();
                ss.Flatten(); // es entstehen Shapes mit Polylinien, das ist für Faces schlecht
                Face fc = Face.MakeFace(new PlaneSurface(plane), ss);
                fc.ColorDef = color;
                res.Add(fc);
            }
            return res;
        }
        public override IShowProperty GetShowProperty()
        {
            return null;
        }
        public override HatchStyle Clone()
        {
            HatchStyleSolid res = new HatchStyleSolid();
            res.Name = base.Name;
            res.color = color;
            return res;
        }
        internal override void Update(bool AddMissingToList)
        {
            if (color != null && Parent != null && Parent.Owner != null)
            {
                ColorList cl = Parent.Owner.ColorList;
                if (cl != null)
                {
                    ColorDef cd = cl.Find(color.Name);
                    if (cd != null)
                        color = cd;
                    else if (AddMissingToList)
                        cl.Add(color);
                }
            }
        }
        #region IPropertyEntry Members
        private IPropertyEntry[] subItems;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    ColorSelectionProperty csp = new ColorSelectionProperty("HatchStyleSolid.Color", Frame.Project.ColorList, this.Color, ColorList.StaticFlags.allowFromParent);
                    csp.ShowAllowUndefinedGray = false;
                    csp.ColorDefChangedEvent += new ColorSelectionProperty.ColorDefChangedDelegate(OnColorDefChanged);
                    subItems = new IPropertyEntry[] { csp };
                }
                return subItems;
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HatchStyleSolid(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            color = (ColorDef)info.GetValue("Color", typeof(ColorDef));
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Color", color);
        }

        #endregion
        private void OnColorDefChanged(ColorDef selected)
        {
            Color = selected;
        }
    }
}
