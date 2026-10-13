using CADability.GeoObject;
using CADability.UserInterface;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a <see cref="Text"/> object placed at a single point. The user picks the location of the text and edits
    /// the text string and its properties (font, size, alignment) in the control center. The new text takes over the settings of
    /// the last text constructed, or uses the default text with the directions of the active drawing plane for the first one.
    /// Started with the menu id "Constr.Text.1Point".
    /// </summary>
    internal class ConstrText1Point : ConstructAction
    {
        private GeoPoint startP;
        private Text text;
        public static Text defaultText;

        public ConstrText1Point()
        { }


        private void StartPoint(GeoPoint p)
        {
            startP = p;
            text.Location = p;
        }


        public override void OnSetAction()
        {
            if (defaultText == null)
            {
                text = Text.Construct();
                text.TextString = StringTable.GetString("Text.Default");
                text.LineDirection = base.ActiveDrawingPlane.DirectionX;
                text.GlyphDirection = base.ActiveDrawingPlane.DirectionY;
                text.TextSize = ConstrDefaults.DefaultTextSize;
                text.Font = "Arial";
            }
            else text = defaultText.Clone() as Text;
            if (!Precision.IsPerpendicular(ActiveDrawingPlane.Normal, text.LineDirection, false) || !Precision.IsPerpendicular(ActiveDrawingPlane.Normal, text.GlyphDirection, false))
            {   // die DrawingPlane wurde seit dem letzten Text geändert
                text.LineDirection = base.ActiveDrawingPlane.DirectionX;
                text.GlyphDirection = base.ActiveDrawingPlane.DirectionY;
            }

            base.ActiveObject = text;

            base.TitleId = "Constr.Text.1Point";

            GeoPointInput startPointInput = new GeoPointInput("Text.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            EditInput editInput = new EditInput(text);
            base.SetInput(startPointInput, editInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Text.1Point";
        }

        public override void OnDone()
        {
            defaultText = text;
            base.OnDone();
        }
    }
}




