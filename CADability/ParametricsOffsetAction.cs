using CADability.Actions;
using CADability.GeoObject;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Actions
{
    /// <summary>
    /// This action modifies two parts of the solid: frontSide and backSide, which are parallel. Typically used to modify the thickness or gauge of a part.
    /// Parametric.OffsetFaces method according to the provided input
    /// </summary>
    internal class ParametricsOffsetAction : ConstructAction
    {
        private HashSet<Face> frontSide;
        private HashSet<Face> backSide;
        private IFrame frame;
        private Shell shell;
        private LengthInput distanceInput; // input filed of the distance
        private MultipleChoiceInput modeInput;
        private enum Mode { forward, symmetric, backward };
        private Mode mode;
        private bool validResult;
        private double gauge;
        private double offset;

        public ParametricsOffsetAction(HashSet<Face> frontSide, HashSet<Face> backSide, IFrame frame, double thickness)
        {
            this.frontSide = frontSide;
            this.backSide = backSide;
            this.frame = frame;
            shell = frontSide.First().Owner as Shell;
            gauge = thickness;
            offset = 0.0;
        }

        public override string GetID()
        {
            return "Constr.Parametrics.Offset";
        }
        public override void OnSetAction()
        {
            base.TitleId = "Constr.Parametrics.DistanceTo";
            FeedBack.AddSelected(frontSide);
            FeedBack.AddSelected(backSide);
            base.ActiveObject = shell.Clone();
            if (shell.Layer != null) shell.Layer.Transparency = 128;


            distanceInput = new LengthInput("DistanceTo.Distance");
            distanceInput.GetLengthEvent += DistanceInput_GetLengthEvent;
            distanceInput.SetLengthEvent += DistanceInput_SetLengthEvent;

            modeInput = new MultipleChoiceInput("DistanceTo.Mode", "DistanceTo.Mode.Values", 0);
            modeInput.SetChoiceEvent += ModeInput_SetChoiceEvent;
            //modeInput.GetChoiceEvent += ModeInput_GetChoiceEvent;
            SetInput(distanceInput, modeInput);
            base.OnSetAction();

            FeedBack.SelectOutline = true;
            validResult = false;
        }
        private int ModeInput_GetChoiceEvent()
        {
            return (int)mode;
        }
        private void ModeInput_SetChoiceEvent(int val)
        {
            mode = (Mode)val;
            DistanceInput_SetLengthEvent(gauge + offset); // this is to refresh only
        }
        private bool DistanceInput_SetLengthEvent(double length)
        {
            validResult = false;
            offset = length - gauge;
            // FeedBack.AddSelected(offsetFeedBack);
            Shell sh = null;
            Parametric pm = new Parametric(shell);
            Dictionary<Face, double> allFacesToOffset = new Dictionary<Face, double>();
            switch (mode)
            {
                case Mode.forward:
                    pm.OffsetFaces(frontSide, offset);
                    break;

                case Mode.symmetric:
                    pm.OffsetFaces(frontSide.Union(backSide), offset / 2.0);
                    break;
                case Mode.backward:
                    pm.OffsetFaces(backSide, offset);
                    break;
            }
            if (pm.Apply())
            {
                sh = pm.Result();
                ActiveObject = sh;
                validResult = true;
                return true;
            }
            else
            {
                ActiveObject = shell.Clone();
                return false;
            }
        }
        private double DistanceInput_GetLengthEvent()
        {
            return gauge + offset;
        }
        public override void OnDone()
        {
            if (validResult && ActiveObject != null)
            {
                Solid sld = shell.Owner as Solid;
                if (sld != null)
                {   // the shell was part of a Solid
                    IGeoObjectOwner owner = sld.Owner; // Model or Block
                    using (Frame.Project.Undo.UndoFrame)
                    {
                        owner.Remove(sld);
                        Solid replacement = Solid.MakeSolid(ActiveObject as Shell);
                        replacement.CopyAttributes(sld);
                        owner.Add(replacement);
                    }
                }
                else
                {
                    IGeoObjectOwner owner = shell.Owner;
                    using (Frame.Project.Undo.UndoFrame)
                    {
                        owner.Remove(shell);
                        owner.Add(ActiveObject);
                    }
                }
            }
            ActiveObject = null;
            base.OnDone();
        }
        public override void OnRemoveAction()
        {
            if (shell.Layer != null) shell.Layer.Transparency = 0; // make the layer opaque again
            base.OnRemoveAction();
        }
    }
}