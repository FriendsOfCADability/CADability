namespace CADability.Actions
{
    /// <summary>
    /// All default values of the construct actions are static members of this class. 
    /// So they can be stored from one construction to the other.
    /// </summary>

    public class ConstrDefaults : ConstructAction
    {
        /// <summary>
        /// The startpoint of a line. Normally it is set at the end of a line-constrution to the endpoint of that line, so a new line can start at the endpoint of the last line by using the TAB-button within the startpoint-textfield to accept the default point.
        /// </summary>
        public static DefaultGeoPoint DefaultStartPoint = new DefaultGeoPoint();
        /// <summary>
        /// The default length of a line. Normally it is set at the end of a line-constrution to the length of that line, so a new line can start with the length of the last line by using the TAB-button within the length-textfield to accept the default length.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultLineLength = new DefaultLength();
        /// <summary>
        /// The default direction of a line. Normally it is set at the end of a line-constrution to the direction of that line, so a new line can start with the direction of the last line by using the TAB-button within the direction-textfield to accept the default direction.
        /// </summary>
        public static ConstructAction.DefaultGeoVector DefaultLineDirection = new DefaultGeoVector();
        /// <summary>
        /// The default angle of a line. Normally it is set at the end of a line-constrution to the angle of that line, so a new line can start with the angle of the last line by using the TAB-button within the angle-textfield to accept the default angle.
        /// </summary>
        public static ConstructAction.DefaultAngle DefaultLineAngle = new DefaultAngle();
        /// <summary>
        /// The default dist of a parallel-line. Normally it is set at the end of a line-constrution to the distance of that line to its parallel source, so a new line can start with the distance of the last line by using the TAB-button within the length-textfield to accept the default angle.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultLineDist = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth8);
        /// <summary>
        /// The default center of a circle or arc. Normally it is set at the end of an arc-constrution to the center of that arc/circle, so a new arc/circle can start with the center of the last arc/circle by using the TAB-button within the center-textfield to accept the default center.
        /// </summary>
        public static ConstructAction.DefaultGeoPoint DefaultArcCenter = new DefaultGeoPoint();
        /// <summary>
        /// The default radius of a circle or arc. Normally it is set at the end of an arc-constrution to the radius of that arc/circle, so a new arc/circle can start with the radius of the last arc/circle by using the TAB-button within the radius-textfield to accept the default radius.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultArcRadius = new DefaultLength();
        /// <summary>
        /// The default diameter of a circle or arc. Normally it is set at the end of an arc-constrution to the diameter of that arc/circle, so a new arc/circle can start with the radius of the last arc/circle by using the TAB-button within the diameter-textfield to accept the default diameter.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultArcDiameter = new DefaultLength();
        /// <summary>
        /// The default direction of an arc. Normally it is set at the end of an arc-constrution to the direction of that arc, so a new arc can start with the direction of the last arc by using the TAB-button within the direction-textfield to accept the default direction.
        /// </summary>
        public static ConstructAction.DefaultBoolean DefaultArcDirection = new DefaultBoolean();
        /// <summary>
        /// The default center of an ellipse or ellipsenarc. Normally it is set at the end of an ellipse-constrution to the center of that ellipse, so a new ellipse can start with the center of the last ellipse by using the TAB-button within the center-textfield to accept the default center.
        /// </summary>
        public static ConstructAction.DefaultGeoPoint DefaultEllipseCenter = new DefaultGeoPoint();
        /// <summary>
        /// The default majorradius of an ellipse or ellipsearc. Normally it is set at the end of an ellipse-constrution to the majorradius of that ellipse(arc), so a new ellipse(arc) can start with the majorradius of the last ellipse(arc) by using the TAB-button within the majorradius-textfield to accept the default majorradius.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultEllipseMajorRadius = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth4);
        /// <summary>
        /// The default minorradius of an ellipse or ellipsearc. Normally it is set at the end of an ellipse-constrution to the minorradius of that ellipse(arc), so a new ellipse(arc) can start with the minorradius of the last ellipse(arc) by using the TAB-button within the minorradius-textfield to accept the default minorradius.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultEllipseMinorRadius = new DefaultLength();
        /// <summary>
        /// The default way of the scale-modification of activated objects. If DefaultDistort is true, there can be different factors in the main axis, which will distort the objects 
        /// </summary>
        public static ConstructAction.DefaultBoolean DefaultScaleDistort = new DefaultBoolean();
        /// <summary>
        /// The default way of the modification of activated objects. If DefaultCopyObjects is true, a copy of the activated objects is modified, if it is false, the original objects are modified.
        /// </summary>
        public static ConstructAction.DefaultBoolean DefaultCopyObjects = new DefaultBoolean();
        /// <summary>
        /// The default radius of the round-actions in the tools-menue. Normally it is set at the end of a round-action to current radius, so a round action can start with the radius of the last action by using the TAB-button within the roundradius-textfield to accept the default radius.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultRoundRadius = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth40);
        /// <summary>
        /// The default width of a rectangle/parallelogram. Normally it is set at the end of a rectangle-constrution to the width of that rectangle, so a new rectangle/parallelogram can start with the width of the last rectangle/parallelogram by using the TAB-button within the width-textfield to accept the default width.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultRectWidth = new DefaultLength();
        /// <summary>
        /// The default height of a rectangle/parallelogram. Normally it is set at the end of a rectangle-constrution to the height of that rectangle, so a new rectangle/parallelogram can start with the height of the last rectangle/parallelogram by using the TAB-button within the height-textfield to accept the default height.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultRectHeight = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth8);
        /// <summary>
        /// The default height of a 3D-box. Normally it is set at the end of a box-constrution to the height of that box, so a new box can start with the height of the last box by using the TAB-button within the height-textfield to accept the default height.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultBoxHeight = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth4);
        /// <summary>
        /// The default method for the way to detect the direction from an object. 0=startpoint, 1=start/endpoint, 2=endpoint, 3=midpoint, 4=direction of mouseposition
        /// </summary>
        public static ConstructAction.DefaultInteger DefaultDirectionPoint = new DefaultInteger();
        /// <summary>
        /// The default method for the way to detect the direction-offset from an object. 0=original, 1=right, 2=opposite, 3=left 
        /// </summary>
        public static ConstructAction.DefaultInteger DefaultDirectionOffset = new DefaultInteger();
        /// <summary>
        /// The default length of the cut-off actions in the tools-menue. Normally it is set at the end of a cut off-action to current length, so a cut off action can start with the length of the last action by using the TAB-button within the cut off length-textfield to accept the default length.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultCutOffLength = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth40);
        /// <summary>
        /// The default angle of the cut-off actions in the tools-menue. Normally it is set at the end of a cut-off action to current angle, so a cut off action can start with the angle of the last action by using the TAB-button within the cut off angle-textfield to accept the default angle.
        /// </summary>
        public static ConstructAction.DefaultAngle DefaultCutOffAngle = new DefaultAngle(ConstructAction.DefaultAngle.StartValue.To45);
        /// <summary>
        /// The default method of the cut-off actions in the tools-menue. Normally it is set at the end of a cut-off action to current method, so a cut off action can start with the method of the last action by using the TAB-button within the cut off angle-textfield to accept the default method.
        /// </summary>
        public static ConstructAction.DefaultInteger DefaultCutOffMethod = new DefaultInteger();
        /// <summary>
        /// The locationpoint of a dimension. Normally it is set at the end of a dimension-constrution to the current position of that dimension, so a new dimension can start at the location point of the last dimension (plus an offset) by using the TAB-button within the dimensionpoint-textfield to accept the default point.
        /// </summary>
        public static DefaultGeoPoint DefaultDimPoint = new DefaultGeoPoint();
        /// <summary>
        /// The default way of collecting and handling points for a dimension ("0" means only 2 Points, "1" means multiple point input, "2" means 2-point-dimension referred to the first point). 
        /// </summary>
        public static ConstructAction.DefaultInteger DefaultDimensionMethod = new DefaultInteger();
        /// <summary>
        /// The default way of collecting a point for a point or labeling dimension ("true" means snap points, "false" means perpendicular foot points of objects). 
        /// </summary>
        public static ConstructAction.DefaultBoolean DefaultDimensionPointMethod = new DefaultBoolean();
        /// <summary>
        /// The default size of the text in text constructions. Normally it is set at the end of a text construction to current size, so next text construction can start with the size of the last construction by using the TAB-button within the cut off length-textfield to accept the default length.
        /// </summary>
        public static ConstructAction.DefaultLength DefaultTextSize = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth40);
        /// <summary>
        /// The default direction of an extrude-action.
        /// </summary>
        public static ConstructAction.DefaultGeoVector DefaultExtrudeDirection = new DefaultGeoVector();
        /// <summary>
        /// The default position a tangent line with direction (Middle or End). 
        /// </summary>
        public static ConstructAction.DefaultBoolean DefaultLinePosition = new DefaultBoolean();

        public static ConstructAction.DefaultLength DefaultExpandDist = new DefaultLength(ConstructAction.DefaultLength.StartValue.Zero);




        /// <summary>
        /// Empty constructor
        /// </summary>
        public ConstrDefaults()
        {
        }
        /// <inheritdoc/>
		public override string GetID()
        {
            return "Constr.Defaults";
        }

    }
}
