using CADability.GeoObject;
using CADability.UserInterface;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    /// <summary>
    /// Sample code for a CADability Action which places a bitmap picture object onto a surface
    /// of some solid object. To use this Action call Frame.SetAction
    /// </summary>
    internal class ConstrPicturePointWidthHeight : ConstructAction
    {
        StringInput fileNameInput; // input field for the filename of the bitmap
        GeoPointInput positionInput; // input field for the position of the bitmap
        LengthInput width; // optional input field for a width factor for the bitmap
        LengthInput height; // optional input field for a height factor for the bitmap
        Picture picture; // the picture object beeing placed
        string fileName; // the filename for the bitmap
        double widthValue; // the width value
        double heightValue; // the height value
        GeoPoint location; // the location of the picture object
        Boolean keepAspectRatioValue;
        Boolean rectangularValue;
        Boolean isOnPlane;
        //        double diagonalPointDist;


        /// <summary>
        /// Must be overriden, returns some ID for the action
        /// </summary>
        /// <returns>The ID</returns>
        public override string GetID()
        {
            return "Constr.Picture.RefPointWidthHeight";
        }

        /// <summary>
        /// Overrides ConstructAction.OnSetAction. Provides the input fields and some initialsation
        /// </summary>
        public override void OnSetAction()
        {

            base.TitleId = "Constr.Picture.RefPointWidthHeight";
            widthValue = 1; // default value
            heightValue = 1; // default value
            keepAspectRatioValue = true;
            rectangularValue = true;
            isOnPlane = false;

            // Create and initialize the filename input field
            fileNameInput = new StringInput("Picture.Object"); // Resource ID must be defined in StringTable
            fileNameInput.IsFileNameInput = true; // to enable the openfile dialog
            fileNameInput.InitOpenFile = true;
            fileNameInput.FileNameFilter = StringTable.GetString("Picture.Open.Filter");
            // you may also wnat to set fileNameInput.FileNameFilter
            fileNameInput.GetStringEvent += new StringInput.GetStringDelegate(OnGetFileName);
            fileNameInput.SetStringEvent += new StringInput.SetStringDelegate(OnSetFileName);
            fileName = ""; // initial filename is empty

            // Create and initialize the position input field
            positionInput = new GeoPointInput("Picture.Location"); // Resource ID must be defined in StringTable
            positionInput.GetGeoPointEvent += new GeoPointInput.GetGeoPointDelegate(OnGetPosition);
            positionInput.SetGeoPointExEvent += new GeoPointInput.SetGeoPointExDelegate(OnSetPosition);
            positionInput.DefinesBasePoint = true;

            width = new LengthInput("Picture.Width");
            width.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(OnGetWidth);
            width.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(OnSetWidth);
            //width.Optional = true; // optinal input
            //width.ReadOnly = true;
            // width.ForwardMouseInputTo = positionInput; // no mouse input, forward to position input


            height = new LengthInput("Picture.Height");
            height.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetHeight);
            height.Optional = true; // optinal input
            //height.ReadOnly = true;
            height.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetHeight);
            height.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(height_CalculateLengthEvent);
            // height.ForwardMouseInputTo = positionInput; // no mouse input, forward to position input

            // the input is the direction
            GeoVectorInput dir = new GeoVectorInput("Picture.Direction");
            // dir.DefaultGeoVector = ConstrDefaults.DefaultLineDirection;
            dir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(SetAngle);
            dir.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetAngle);
            dir.IsAngle = true;
            dir.Optional = true;
            // during angle-input the mouseinput goes to startPointInput, if it isn´t fixed
            dir.ForwardMouseInputTo = positionInput;



            BooleanInput keepAspectRatio = new BooleanInput("Picture.KeepAspectRatio", "YesNo.Values", keepAspectRatioValue);
            keepAspectRatio.SetBooleanEvent += new ConstructAction.BooleanInput.SetBooleanDelegate(OnKeepAspectRatioChanged);

            BooleanInput rectangular = new BooleanInput("Picture.Rectangular", "YesNo.Values", rectangularValue);
            rectangular.SetBooleanEvent += new ConstructAction.BooleanInput.SetBooleanDelegate(OnRectangularChanged);



            // picture must exist prior to SetInput
            // because picture.Location is required by positionInput
            picture = Picture.Construct();

            // Define the input fields for the ConstructAction
            base.SetInput(fileNameInput, positionInput, width, height, dir, keepAspectRatio, rectangular);
            base.OnSetAction(); // Default implementation must be called

            // force the snapmode to include SnapToFaceSurface
            // base.Frame.SnapMode |= SnapPointFinder.SnapModes.SnapToFaceSurface;
        }

        GeoVector GetAngle()
        {
            return picture.DirectionWidth;
        }

        /// <summary>
        /// Called by the filename input field when the filename changes
        /// </summary>
        /// <param name="val">The new file name</param>
        void OnSetFileName(string val)
        {
            fileName = val; // save the name
            Bitmap bmp = Picture.CopyFrom(fileName); // load the bitmap
            picture.Bitmap = bmp; // set the bitmap to the picture object
            picture.Path = fileName; // set the name to the picture object
            base.ActiveObject = picture; // set the active object:
                                         // now that we have a bitmap we set this object as the active object, which means
                                         // that is will be displayed while the location is positioned and at the end of the
                                         // action it will automatically be inserted into the model
#if !WEBASSEMBLY
            widthValue = picture.Bitmap.PhysicalDimension.Width;
            heightValue = picture.Bitmap.PhysicalDimension.Height;
#endif
            picture.DirectionWidth = ActiveDrawingPlane.DirectionX;
            picture.DirectionHeight = ActiveDrawingPlane.DirectionY;
        }

        /// <summary>
        /// Called by the filename input field when the filename string is required
        /// </summary>
        /// <returns>the filename</returns>
        string OnGetFileName()
        {
            return fileName;
        }


        /// <summary>
        /// Called when the position input is changed
        /// </summary>
        /// <param name="p">The new location point</param>
        /// <param name="didSnap">Information on point snapping</param>
        /// <returns>true, if point is accepted</returns>
        bool OnSetPosition(GeoPoint p, SnapPointFinder.DidSnapModes didSnap)
        {
            location = p;
            // default vectors for the two sides of the bitmap
            GeoVector dirWidth = picture.DirectionWidth.Normalized;
            GeoVector dirHeight = picture.DirectionHeight.Normalized;
            if (didSnap == SnapPointFinder.DidSnapModes.DidSnapToFaceSurface)
            {   // if there was a snap on the surface of a face we want to center the picture object
                // there. We also want to make it parallel to the surface at this point and stand upright.
                Face fc = base.LastSnapObject as Face; // this object was involved in the snapping
                if (fc != null) // should always be the case
                {
                    GeoPoint2D pos = fc.Surface.PositionOf(location); // position in the surface u/v system
                    GeoVector normal = fc.Surface.GetNormal(pos); // normal vector at this position
                    Projection projection = base.CurrentMouseView.Projection; // the projection of the view
                    if (projection != null)
                    {   // make sure that the normal vector points away from the user.
                        // On faces not in a solid both sides of the face are displayed and you don't know
                        // on which side you are
                        if (projection.Direction * normal < 0.0) normal = -normal;
                    }
                    location = location - 0.001 * normal.Normalized; // moves the location point a little bit 
                    // in the direction of the normal vector to the user so
                    // that the bitmap hovers a little above the surface to avoid display artefacts
                    if (Precision.SameDirection(normal, GeoVector.ZAxis, false))
                    {   // the surface is parallel to the x/y plane: orient the bitmap to the x/y axis
                        dirWidth = GeoVector.XAxis;
                        dirHeight = GeoVector.YAxis;
                    }
                    else
                    {   // some arbitrary surface direction: calculate the base direction of the bitmap to
                        // be parallel to the x/y plane and the up direction rectangular to the base and the normal.
                        // this makes the bitmap appear upright (in the sense of the z-axis)
                        dirWidth = normal ^ GeoVector.ZAxis;
                        dirHeight = dirWidth ^ normal;
                        dirWidth.Norm(); // we need normalized vectors
                        dirHeight.Norm();
                    }
                    picture.Location = location - 0.5 * widthValue * dirWidth - 0.5 * heightValue * dirHeight;
                    isOnPlane = true;
                }
            }
            else picture.Location = location;
            // provide the picture object with proper aspect ratio and scaling according to the scaling factor
            picture.DirectionWidth = widthValue * dirWidth;
            picture.DirectionHeight = heightValue * dirHeight;
            // set the location of the object so that the center of the bitmap occures at the position of location
            // Since picture.Location is the lower left point of the object we must move it left and down
            // with half of it's size
            // diagonalPointDist = Geometry.Dist(picture.Location, picture.Location + picture.DirectionWidth + picture.DirectionHeight);
            return true; // this was OK
        }

        /// <summary>
        /// Called by the position input field to get the current location
        /// </summary>
        /// <returns></returns>
        GeoPoint OnGetPosition()
        {
            return location;
        }


        private bool OnSetWidth(double l)
        {
            if (l > Precision.eps)
            {
                widthValue = l;
                //                scalingFactor = 1.0;
                if (isOnPlane)
                {
                    GeoVector dirWidth = picture.DirectionWidth.Normalized;
                    GeoVector dirHeight = picture.DirectionHeight.Normalized;
                    widthValue = 2 * l;
                    picture.Location = base.BasePoint - 0.5 * widthValue * dirWidth - 0.5 * heightValue * dirHeight;

                }
                picture.DirectionWidth = widthValue * picture.DirectionWidth.Normalized;
                if (keepAspectRatioValue)
                {
                    picture.DirectionHeight = l * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
                    heightValue = picture.DirectionHeight.Length;
                }
                return true;
            }
            return false;
        }

        private double OnGetWidth()
        {
            return picture.DirectionWidth.Length;
        }

        private bool OnSetHeight(double l)
        {
            if (l > Precision.eps)
            {
                heightValue = l;
                picture.DirectionHeight = heightValue * picture.DirectionHeight.Normalized;
                if (keepAspectRatioValue)
                {
                    picture.DirectionWidth = l * picture.Bitmap.Width / picture.Bitmap.Height * picture.DirectionWidth.Normalized;
                    widthValue = picture.DirectionWidth.Length;
                }
                return true;
            }
            return false;
        }

        double height_CalculateLengthEvent(GeoPoint MousePosition)
        {
            if (positionInput.Fixed)
            {
                return (Geometry.Dist(MousePosition, Geometry.DropPL(MousePosition, picture.Location, picture.DirectionWidth)));

            }
            return picture.DirectionHeight.Length;
        }
        private double OnGetHeight()
        {
            return picture.DirectionHeight.Length;
        }

        private bool SetAngle(GeoVector vector)
        {
            if (Precision.IsNullVector(vector)) return false;
            vector.Norm();
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            picture.DirectionWidth = picture.DirectionWidth.Length * vector;
            picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ vector).Normalized;
            return true;
        }



        void OnKeepAspectRatioChanged(bool NewValue)
        {
            keepAspectRatioValue = NewValue;
            if (NewValue)
            {
                picture.DirectionHeight = picture.DirectionWidth.Length * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
                height.Optional = true;
            }
            else height.Optional = false;

        }

        void OnRectangularChanged(bool NewValue)
        {
            rectangularValue = NewValue;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            if (NewValue)
            {
                picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ picture.DirectionWidth).Normalized;
                height.Optional = true;
            }
            else height.Optional = false;
        }


        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnActivate (Action, bool)"/>
        /// </summary>
        /// <param name="OldActiveAction"></param>
        /// <param name="SettingAction"></param>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            base.OnActivate(OldActiveAction, SettingAction);
            if (fileName == "")
            {
                base.OnEscape();
            }
        }
    }

}
