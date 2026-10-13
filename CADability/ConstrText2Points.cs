using CADability.GeoObject;
using CADability.UserInterface;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a <see cref="Text"/> object whose base line is given by two points. The user enters
    /// the start point and the second point; the text is centered between them, its base line follows the
    /// direction of the two points and keeps its length (and so the text size), the third input edits the
    /// text string and its font properties. The text of the last construction is the default for the next
    /// one. Started with the action id "Constr.Text.2Points" (menu command "MenuId.Constr.Text.2Points").
    /// </summary>
    internal class ConstrText2Points : ConstructAction
    {
        private GeoPoint startP, endP;
        private Text text;
        private GeoVector2D textDir2D;
        private double textLen;
        private GeoPointInput startPointInput;
        private GeoPointInput secondPointInput;
        private CADability.GeoObject.Point gPoint;
        public static Text defaultText;

        public ConstrText2Points()
        { }


        private void StartPoint(GeoPoint p)
        {
            startP = p;
            if (secondPointInput.Fixed)
            {
                if (!Precision.IsEqual(endP, p))
                {
                    text.Location = new GeoPoint(p, endP);
                    GeoVector v = new GeoVector(p, endP);
                    //					v.Length = textLen;
                    v.Length = text.LineDirection.Length;
                    text.LineDirection = v;
                    Plane pl = new Plane(text.Location, text.LineDirection, base.ActiveDrawingPlane.Normal ^ text.LineDirection);
                    text.GlyphDirection = pl.ToGlobal(textDir2D);
                }
            }
            else
            {
                text.Location = p;
                gPoint.Location = p;
            }
        }

        private void SecondPoint(GeoPoint p)
        {
            endP = p;
            if (startPointInput.Fixed)
            {
                if (!Precision.IsEqual(startP, p))
                {
                    text.Location = new GeoPoint(startP, p);
                    GeoVector v = new GeoVector(startP, p);
                    //					v.Length = textLen;
                    v.Length = text.LineDirection.Length;
                    text.LineDirection = v;
                    Plane pl = new Plane(text.Location, text.LineDirection, base.ActiveDrawingPlane.Normal ^ text.LineDirection);
                    text.GlyphDirection = pl.ToGlobal(textDir2D);
                }
            }
            else
            {
                text.Location = p;
                gPoint.Location = p;
            }
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
            text.LineAlignment = Text.LineAlignMode.Center;
            text.Location = ConstrDefaults.DefaultStartPoint;
            Plane pl = new Plane(text.Location, text.LineDirection, text.GlyphDirection);
            textDir2D = pl.Project(text.GlyphDirection);
            textLen = text.LineDirection.Length;

            base.ActiveObject = text;

            gPoint = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint);

            base.TitleId = "Constr.Text.2Points";

            startPointInput = new GeoPointInput("Text.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            secondPointInput = new GeoPointInput("Text.SecondPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SecondPoint);

            EditInput editInput = new EditInput(text);

            base.SetInput(startPointInput, secondPointInput, editInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Text.2Points";
        }

        public override void OnDone()
        {
            defaultText = text;
            base.OnDone();
        }
    }
}



