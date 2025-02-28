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
    internal class ConstrPicturePoint2Directions : ConstructAction
    {
        StringInput fileNameInput; // input field for the filename of the bitmap
        GeoPointInput positionInput; // input field for the position of the bitmap
        LengthInput width; // optional input field for a width factor for the bitmap
        LengthInput height; // optional input field for a height factor for the bitmap
        GeoVectorInput dirHeight;
        GeoVectorInput dirWidth;
        Picture picture; // the picture object beeing placed
        string fileName; // the filename for the bitmap
        double widthValue; // the width value
        double heightValue; // the height value
        GeoPoint location; // the location of the picture object
        Boolean keepAspectRatioValue;
        Boolean rectangularValue;
        //        double diagonalPointDist;


        /// <summary>
        /// Must be overriden, returns some ID for the action
        /// </summary>
        /// <returns>The ID</returns>
        public override string GetID()
        {
            return "Constr.Picture.RefPoint2Directions";
        }

        /// <summary>
        /// Overrides ConstructAction.OnSetAction. Provides the input fields and some initialsation
        /// </summary>
        public override void OnSetAction()
        {

            base.TitleId = "Constr.Picture.RefPoint2Directions";
            widthValue = 1; // default value
            heightValue = 1; // default value
            keepAspectRatioValue = true;
            rectangularValue = true;

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

            dirWidth = new GeoVectorInput("Picture.DirWidth");
            dirWidth.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(OnGetDirWidth);
            dirWidth.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(OnSetDirWidth);
            dirWidth.CalculateGeoVectorEvent += new GeoVectorInput.CalculateGeoVectorDelegate(dirWidth_CalculateGeoVectorEvent);
            // dirWidth.Optional = true;

            dirHeight = new GeoVectorInput("Picture.DirHeight");
            dirHeight.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(OnGetDirHeight);
            dirHeight.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(OnSetDirHeight);
            dirHeight.CalculateGeoVectorEvent += new GeoVectorInput.CalculateGeoVectorDelegate(dirHeight_CalculateGeoVectorEvent);
            dirHeight.Optional = true;

            BooleanInput keepAspectRatio = new BooleanInput("Picture.KeepAspectRatio", "YesNo.Values", keepAspectRatioValue);
            keepAspectRatio.SetBooleanEvent += new ConstructAction.BooleanInput.SetBooleanDelegate(OnKeepAspectRatioChanged);

            BooleanInput rectangular = new BooleanInput("Picture.Rectangular", "YesNo.Values", rectangularValue);
            rectangular.SetBooleanEvent += new ConstructAction.BooleanInput.SetBooleanDelegate(OnRectangularChanged);


            width = new LengthInput("Picture.Width");
            width.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(OnGetWidth);
            //width.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(OnSetWidth);
            width.Optional = true; // optinal input
            width.ReadOnly = true;
            // width.ForwardMouseInputTo = positionInput; // no mouse input, forward to position input


            height = new LengthInput("Picture.Height");
            height.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetHeight);
            height.Optional = true; // optinal input
            height.ReadOnly = true;
            //height.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetHeight);
            // height.ForwardMouseInputTo = positionInput; // no mouse input, forward to position input


            // Create and initialize the scaling factor input field
            //scalingFactorInput = new DoubleInput("Picture.Scale"); // Resource ID must be defined in StringTable
            //scalingFactorInput.GetDoubleEvent += new DoubleInput.GetDoubleDelegate(OnGetScalingFactor);
            //scalingFactorInput.SetDoubleEvent += new DoubleInput.SetDoubleDelegate(OnSetScalingFactor);
            //scalingFactorInput.CalculateDoubleEvent += new DoubleInput.CalculateDoubleDelegate(CalculateScalingFactorEvent);
            //scalingFactorInput.Optional = true; // optinal input
            //scalingFactorInput.ReadOnly = true;
            //scalingFactorInput.ForwardMouseInputTo = positionInput; // no mouse input, forward to position input
            //scalingFactor = 1.0; // default scaling factor



            // picture must exist prior to SetInput
            // because picture.Location is required by positionInput
            picture = Picture.Construct();

            // Define the input fields for the ConstructAction
            base.SetInput(fileNameInput, positionInput, dirWidth, dirHeight, keepAspectRatio, rectangular, width, height);
            base.OnSetAction(); // Default implementation must be called

            // force the snapmode to include SnapToFaceSurface
            // base.Frame.SnapMode |= SnapPointFinder.SnapModes.SnapToFaceSurface;
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
            GeoVector dirWidth = ActiveDrawingPlane.DirectionX;
            GeoVector dirHeight = ActiveDrawingPlane.DirectionY;
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
                    //                    picture.Location = location - 0.5 * widthValue * dirWidth - 0.5 * heightValue * dirHeight;
                    picture.Location = location;
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

        private bool OnSetDirWidth(GeoVector v)
        {
            if (Precision.IsNullVector(v)) return false;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            picture.DirectionWidth = v;
            if (rectangularValue)
            {
                picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ v).Normalized;
            }
            if (keepAspectRatioValue)
            {
                picture.DirectionHeight = picture.DirectionWidth.Length * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
            }
            return true;
        }

        GeoVector dirWidth_CalculateGeoVectorEvent(GeoPoint MousePosition)
        {
            if (dirHeight.Fixed && rectangularValue && !keepAspectRatioValue)
            {
                return new GeoVector(Geometry.DropPL(MousePosition, picture.Location, picture.DirectionHeight), MousePosition);
            }
            else return new GeoVector(picture.Location, MousePosition);
        }

        private GeoVector OnGetDirWidth()
        {
            return picture.DirectionWidth;
        }

        private bool OnSetDirHeight(GeoVector v)
        {
            if (Precision.IsNullVector(v)) return false;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            picture.DirectionHeight = v;
            if (rectangularValue)
            {
                picture.DirectionWidth = picture.DirectionWidth.Length * (v ^ normal).Normalized;
            }
            if (keepAspectRatioValue)
            {
                picture.DirectionWidth = picture.DirectionHeight.Length / picture.Bitmap.Height * picture.Bitmap.Width * picture.DirectionWidth.Normalized;
            }
            return true;
        }

        GeoVector dirHeight_CalculateGeoVectorEvent(GeoPoint MousePosition)
        {
            if (dirWidth.Fixed && rectangularValue && !keepAspectRatioValue)
            {
                return new GeoVector(Geometry.DropPL(MousePosition, picture.Location, picture.DirectionWidth), MousePosition);
            }
            else return new GeoVector(picture.Location, MousePosition);
        }


        private GeoVector OnGetDirHeight()
        {
            return picture.DirectionHeight;
        }

        void OnKeepAspectRatioChanged(bool NewValue)
        {
            keepAspectRatioValue = NewValue;
            if (NewValue)
            {
                picture.DirectionHeight = picture.DirectionWidth.Length * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
                dirHeight.Optional = true;
            }
            else dirHeight.Optional = false;

        }

        void OnRectangularChanged(bool NewValue)
        {
            rectangularValue = NewValue;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            if (NewValue)
            {
                picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ picture.DirectionWidth).Normalized;
                dirHeight.Optional = true;
            }
            else dirHeight.Optional = false;
        }

        private double OnGetWidth()
        {
            return picture.DirectionWidth.Length;
        }

        private double OnGetHeight()
        {
            return picture.DirectionHeight.Length;
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



        //double CalculateScalingFactorEvent(GeoPoint MousePosition)
        //{
        //    if (positionInput.Fixed)
        //    {
        //        return (Geometry.Dist(MousePosition, location) / diagonalPointDist);

        //    }
        //    return (1.0);
        //    return scalingFactor;
        //}

        /// <summary>
        /// Will be called when some number is entered into the scaling factor input field
        /// </summary>
        /// <param name="val">The new value</param>
        /// <returns>true, if accepted</returns>
        //bool OnSetScalingFactor(double val)
        //{
        //    if (val > Precision.eps) // accept only positive values
        //    {
        //        scalingFactor = val; // save value
        //        picture.DirectionWidth = val * widthValue * picture.DirectionWidth.Normalized;
        //        picture.DirectionHeight = val * heightValue * picture.DirectionHeight.Normalized;
        //        return true;
        //    }
        //    return false;
        //}

        /// <summary>
        /// Called by the scaling factor input field to determine which number to display
        /// </summary>
        /// <returns></returns>
        //double OnGetScalingFactor()
        //{
        //    return scalingFactor;
        //}


        //private bool OnSetWidth(double l)
        //{
        //    if (l > Precision.eps)
        //    {
        //        widthValue = l;
        //        scalingFactor = 1.0;
        //        picture.DirectionWidth = widthValue * picture.DirectionWidth.Normalized;
        //        if (keepAspectRatioValue)
        //        {
        //            picture.DirectionHeight = l * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
        //            heightValue = picture.DirectionHeight.Length;
        //        }
        //        return true;
        //    }
        //    return false;
        //}
        //private bool OnSetHeight(double l)
        //{
        //    if (l > Precision.eps)
        //    {
        //        heightValue = l;
        //        scalingFactor = 1.0;
        //        picture.DirectionHeight = heightValue * picture.DirectionHeight.Normalized;
        //        if (keepAspectRatioValue)
        //        {
        //            picture.DirectionWidth = l * picture.Bitmap.Width / picture.Bitmap.Height * picture.DirectionWidth.Normalized;
        //            widthValue = picture.DirectionWidth.Length;
        //        }
        //        return true;
        //    }
        //    return false;
        //}

    }
}
