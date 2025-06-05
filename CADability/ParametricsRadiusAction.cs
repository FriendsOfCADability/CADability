using CADability.Actions;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace CADability.Actions
{
    /// <summary>
    /// This class is the interactive way (<seealso cref="ConstructAction"/>) to define a <see cref="ParametricRadiusProperty"/>.
    /// It is constructed with a list of faces, which have a Radius-property. The input of a radius or diameter creates a <see cref="Parametric"/> for this
    /// shell and calls <see cref="Parametric.ModifyRadius(IEnumerable{Face}, double)"/> or <see cref="Parametric.ModifyFilletRadius(Face[], double)"/> to
    /// apply these changes. Also a <see cref="ParametricRadiusProperty"/> is created. If a name is provided for this property, it will be attached to the shell,
    /// so it can later be modified in the property grid of the solid, which wraps the shell.
    /// </summary>
    public class ParametricsRadiusAction : ConstructAction
    {
        private Face[] facesWithRadius;
        private IFrame frame;
        private bool diameter;
        private Shell shell;
        private LengthInput radiusInput;
        private LengthInput diameterInput;
        private bool validResult;
        private bool useRadius;
        private bool isFillet;
        private string parametricsName;
        private ParametricRadiusProperty parametricProperty;
        private BooleanInput preserveInput;

        public ParametricsRadiusAction(Face[] facesWithRadius, IFrame frame, bool useRadius)
        {
            this.facesWithRadius = facesWithRadius;
            this.frame = frame;
            this.useRadius = useRadius;
            diameter = !useRadius;
            shell = facesWithRadius[0].Owner as Shell;
            // Prametrics class has a problem with subdevided edges, which are connected in CombineConnectedFaces via CombineConnectedSameSurfaceEdges
            // we call this here, because during the action there is a problem with changing a GeoObject of the model and continuous changes.
            if (!shell.State.HasFlag(Shell.ShellFlags.FacesCombined)) shell.CombineConnectedFaces();
            isFillet = facesWithRadius[0].IsFillet();
        }

        public override string GetID()
        {
            return "MenuId.Parametrics.Cylinder";
        }

        public override bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Parametrics.Cylinder.Radius":
                    diameter = false;
                    frame.SetAction(this); // this is the way this action comes to life
                    return true;
                case "MenuId.Parametrics.Cylinder.Diameter":
                    diameter = true;
                    frame.SetAction(this); // this is the way this action comes to life
                    return true;
            }
            return false;
        }

        public override void OnSelected(MenuWithHandler selectedMenuItem, bool selected)
        {

        }

        public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Parametrics.Cylinder.Radius":
                    return true;
                case "MenuId.Parametrics.Cylinder.Diameter":
                    return true;
            }
            return false;
        }

        public override void OnSetAction()
        {
            if (useRadius) base.TitleId = "Constr.Parametrics.Cylinder.Radius";
            else base.TitleId = "Constr.Parametrics.Cylinder.Diameter";
            base.ActiveObject = shell.Clone();
            List<InputObject> actionInputs = new List<InputObject>();

            if (useRadius)
            {
                radiusInput = new LengthInput("Parametrics.Cylinder.Radius");
                radiusInput.GetLengthEvent += RadiusInput_GetLength;
                radiusInput.SetLengthEvent += RadiusInput_SetLength;
                // radiusInput.Optional = diameter;
                actionInputs.Add(radiusInput);
            }
            else
            {
                diameterInput = new LengthInput("Parametrics.Cylinder.Diameter");
                diameterInput.GetLengthEvent += DiameterInput_GetLength;
                diameterInput.SetLengthEvent += DiameterInput_SetLength;
                // diameterInput.Optional = !diameter;
                actionInputs.Add(diameterInput);
            }

            SeparatorInput separator = new SeparatorInput("Parametrics.AssociateParametric");
            actionInputs.Add(separator);
            StringInput nameInput = new StringInput("Parametrics.ParametricsName");
            nameInput.SetStringEvent += NameInput_SetStringEvent;
            nameInput.GetStringEvent += NameInput_GetStringEvent;
            nameInput.Optional = true;
            actionInputs.Add(nameInput);

            preserveInput = new BooleanInput("Parametrics.PreserveValue", "YesNo.Values", false);
            actionInputs.Add(preserveInput);
            SetInput(actionInputs.ToArray());
            base.OnSetAction();

            validResult = false;
        }
        private string NameInput_GetStringEvent()
        {
            if (parametricsName == null) return string.Empty;
            else return parametricsName;
        }

        private void NameInput_SetStringEvent(string val)
        {
            parametricsName = val;
        }
        public override void OnActivate(Actions.Action OldActiveAction, bool SettingAction)
        {
            if (shell.Layer != null) shell.Layer.Transparency = 128;
            base.OnActivate(OldActiveAction, SettingAction);
        }
        public override void OnInactivate(Actions.Action NewActiveAction, bool RemovingAction)
        {
            if (shell.Layer != null) shell.Layer.Transparency = 0;
            base.OnInactivate(NewActiveAction, RemovingAction);
        }
        private bool RadiusInput_SetLength(double length)
        {
            validResult = false;
            if (shell != null && length > 0.0)
            {
                Parametric pm = new Parametric(shell);
                bool ok = false;
                if (isFillet) ok = pm.ModifyFilletRadius(facesWithRadius, length);
                else ok = pm.ModifyRadius(facesWithRadius, length);
                if (ok)
                {
                    if (pm.Apply())
                    {
                        Shell sh = pm.Result();
                        ActiveObject = sh;
                        validResult = true;
                        pm.GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict);
                        parametricProperty = new ParametricRadiusProperty("", Extensions.LookUp(facesWithRadius, faceDict), diameter, pm.GetAffectedObjects());
                        return true;
                    }
                    else
                    {
                        ActiveObject = shell.Clone();
                        return false;
                    }
                }
            }
            return false;
        }

        private double RadiusInput_GetLength()
        {
            if (facesWithRadius[0].Surface is ICylinder cyl) return cyl.Radius; // we only have round cylinders here
            else return 0.0;
        }
        private bool DiameterInput_SetLength(double length)
        {
            bool ok = RadiusInput_SetLength(length / 2.0);
            if (ok) return true;
            else
            {
                diameterInput.SetError(StringTable.GetString("Parametrics.InvalidPropertyValue", StringTable.Category.label));
                return false;
            }
        }

        private double DiameterInput_GetLength()
        {
            return 2.0 * RadiusInput_GetLength();
        }

        public override void OnDone()
        {
            if (validResult && ActiveObject != null)
            {
                using (Frame.Project.Undo.UndoFrame)
                {
                    Solid sld = shell.Owner as Solid;
                    if (sld != null)
                    {   // the shell was part of a Solid
                        IGeoObjectOwner owner = sld.Owner; // Model or Block
                        owner.Remove(sld);
                        Solid replacement = Solid.MakeSolid(ActiveObject as Shell);
                        owner.Add(replacement);
                        if (!string.IsNullOrEmpty(parametricsName) && parametricProperty != null)
                        {
                            parametricProperty.Name = parametricsName;
                            parametricProperty.Preserve = preserveInput.Value;
                            replacement.Shells[0].AddParametricProperty(parametricProperty);
                        }
                    }
                    else
                    {
                        IGeoObjectOwner owner = shell.Owner;
                        owner.Remove(shell);
                        owner.Add(ActiveObject);
                    }
                }
            }
            ActiveObject = null;
            base.OnDone();
        }
    }
}
