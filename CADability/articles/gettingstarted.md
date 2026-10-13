# Getting started

CADability is a .NET class library for 3d CAD: it reads and writes CAD files, holds the geometry in a model,
builds and combines solids, and, with CADability.Forms, displays and edits the model in a Windows.Forms
control. This article shows the first steps as short recipes. The classes mentioned are described in
the [API documentation](../api/index.md), the ideas behind them in the [introduction](general.md) and the
[data model](database.md).

## Setup

The kernel is the NuGet package [CADability](https://www.nuget.org/packages/CADability) (netstandard2.0,
so it works in .NET Framework 4.8 as well as in .NET 8 and later), with everything but the user interface:

```xml
<PackageReference Include="CADability" Version="1.6.0" />
```

The user interface, CADability.Forms, is not on NuGet. Clone the repository with its submodules and
reference `CADability.Forms/CADability.Forms.csproj` from your Windows.Forms project (it targets
net8.0-windows; the kernel is referenced through it):

```
git clone --recurse-submodules https://github.com/FriendsOfCADability/CADability.git
```

Most of the types used below live in the namespaces `CADability` (Project, Model, GeoPoint, GeoVector,
Plane, Settings), `CADability.GeoObject` (the geometric objects: Solid, Shell, Face, Line, Path, Make3D...)
and `CADability.Attribute` (Layer, ColorDef and the other attributes). `Path` clashes with `System.IO.Path`
when both namespaces are imported, an alias resolves that:

```csharp
using CADability;
using CADability.Attribute;
using CADability.GeoObject;
using Path = CADability.GeoObject.Path;
```

CADability does not know about units: a length of 1 is whatever you decide, millimetres usually.

## Read a file

[Project.ReadFromFile(string, string)](xref:CADability.Project.ReadFromFile(System.String,System.String))
reads a STEP, DXF, DWG or STL file, or a file in CADability's own format, into a new
[Project](xref:CADability.Project). The second parameter names the format ("step", "stp", "dxf", "dwg",
"stl" or "cdb"), the extension of the file name is not used:

```csharp
Project project = Project.ReadFromFile(@"C:\data\bracket.stp", "step");
if (project == null) throw new Exception("not a supported format");
Model model = project.GetActiveModel();
Console.WriteLine($"{model.Count} objects, extent {model.Extent.Size:F1}");
```

A project contains one or more [Model](xref:CADability.Model)s, the views on them, and the lists of
attributes (layers, colours, line widths, styles...) the objects refer to. The imports put everything into
the active model. DXF and DWG files also bring their layers, blocks and line styles along, STEP files their
assembly structure (as [Block](xref:CADability.GeoObject.Block)s) and the product names.

## Look at the objects of a model

A model is a list of [IGeoObject](xref:CADability.GeoObject.IGeoObject)s: solids, shells, faces, curves,
text, dimensions, hatches, and blocks that group other objects. Test for the types you are interested in:

```csharp
foreach (IGeoObject go in model)
{
    switch (go)
    {
        case Solid solid:
            Console.WriteLine($"solid {solid.Name}: {solid.Shells[0].Faces.Length} faces, volume {solid.Volume(0.01):F1}");
            break;
        case Shell shell: // faces that do not enclose a volume
            Console.WriteLine($"shell {shell.Name}: {shell.Faces.Length} faces, {shell.OpenEdges.Length} open edges");
            break;
        case Block block: // an assembly from a STEP file, or a block reference from DXF
            Console.WriteLine($"block {block.Name} with {block.Count} children");
            break;
        case ICurve curve: // Line, Ellipse (also circles and arcs), BSpline, Polyline, Path
            Console.WriteLine($"{go.GetType().Name} from {curve.StartPoint} to {curve.EndPoint}, length {curve.Length:F1}");
            break;
    }
    Console.WriteLine($"    layer {go.Layer?.Name}, colour {(go as IColorDef)?.ColorDef?.Name}, extent {go.GetBoundingCube()}");
}
```

Blocks contain their objects in `Children`; the children of a block are not in the model's list, so
recurse into the blocks when you need every solid of an assembly. A solid consists of
[Shell](xref:CADability.GeoObject.Shell)s (the outer hull, and one shell for every cavity),
a shell of [Face](xref:CADability.GeoObject.Face)s, a face of a surface and its
[Edge](xref:CADability.Edge)s; see the article on the
[boundary representation](brep.md) for how they fit together.

## Build solids

Without a user interface start with [Project.CreateSimpleProject](xref:CADability.Project.CreateSimpleProject),
which gives you a project with one empty model and the standard attribute lists. The static methods of
[Make3D](xref:CADability.GeoObject.Make3D) create solids: primitives, and extrusions and revolutions of
planar outlines or faces.

```csharp
Project project = Project.CreateSimpleProject();
Model model = project.GetActiveModel();

// primitives: a box with the edges along the axes, and a cylinder (centre of the bottom circle, radius vector, axis with its length as height)
Solid box = Make3D.MakeBox(GeoPoint.Origin, 100 * GeoVector.XAxis, 60 * GeoVector.YAxis, 40 * GeoVector.ZAxis);
Solid cylinder = Make3D.MakeCylinder(new GeoPoint(50, 30, -10), 15 * GeoVector.XAxis, 60 * GeoVector.ZAxis);

// a closed planar outline: a rectangle with a rounded corner
Ellipse arc = Ellipse.Construct();
arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(35, 25, 0), 5, 0.0, Math.PI / 2);
Path outline = Path.FromSegments(new ICurve[] {
    Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(40, 0, 0)),
    Line.TwoPoints(new GeoPoint(40, 0, 0), new GeoPoint(40, 25, 0)),
    arc,
    Line.TwoPoints(new GeoPoint(35, 30, 0), new GeoPoint(0, 30, 0)),
    Line.TwoPoints(new GeoPoint(0, 30, 0), new GeoPoint(0, 0, 0)) }, true);

// extrude the outline: a prism of height 20
Solid prism = (Solid)Make3D.MakePrism(outline, 20 * GeoVector.ZAxis, project, false);

// rotate a face around the y-axis: the face of the outline, moved 50 along x, swept by a full turn
Face profile = Face.MakeFace(new GeoObjectList(outline));
profile.Modify(ModOp.Translate(50, 0, 0));
Solid ring = (Solid)Make3D.MakeRevolution(profile, GeoPoint.Origin, GeoVector.YAxis, 2 * Math.PI, project);

model.Add(prism);
model.Add(ring);
```

Points and vectors are the structs [GeoPoint](xref:CADability.GeoPoint) and
[GeoVector](xref:CADability.GeoVector); `ModOp` is a transformation (translation, rotation, scaling)
that every object applies to itself with `Modify`. Methods that take a project set the project's current
layer and colour on their result. Objects created without a project have none; give them the defaults
with [Project.SetDefaults](xref:CADability.Project.SetDefaults(CADability.GeoObject.IGeoObject)) before
you add them, otherwise they are drawn in the default colour and belong to no layer:

```csharp
project.SetDefaults(box);
model.Add(box);
```

## Boolean operations

The static methods [Solid.Unite](xref:CADability.GeoObject.Solid.Unite(CADability.GeoObject.Solid,CADability.GeoObject.Solid)),
[Solid.Subtract](xref:CADability.GeoObject.Solid.Subtract(CADability.GeoObject.Solid,CADability.GeoObject.Solid)) and
[Solid.Intersect](xref:CADability.GeoObject.Solid.Intersect(CADability.GeoObject.Solid,CADability.GeoObject.Solid))
return new solids and leave their operands as they are. Subtracting and intersecting may split a solid
into several parts, so they return arrays; the union returns null when the solids do not touch.

```csharp
Solid[] drilled = Solid.Subtract(box, cylinder);  // the box with a hole
Solid[] core = Solid.Intersect(box, cylinder);    // the part of the cylinder inside the box
Solid joined = Solid.Unite(box, cylinder);        // box and cylinder as one solid, or null

foreach (Solid s in drilled)
{
    s.CopyAttributes(box); // or project.SetDefaults(s)
    model.Add(s);
}
```

The operands must be closed solids; an imported shell with open edges (`shell.OpenEdges.Length > 0`)
is not a solid. The results are solids with the faces of the operands, split and trimmed at the
intersection curves, not a triangle mesh.

## Save and export

[Project.WriteToFile](xref:CADability.Project.WriteToFile(System.String)) saves the project in
CADability's own JSON format, with everything in it (all models, views and attributes). Use the
extension `.cdb.json`; [Project.ReadFromFile(string)](xref:CADability.Project.ReadFromFile(System.String))
reads it back. [Project.Export](xref:CADability.Project.Export(System.String,System.String)) writes the
exchange formats; the second parameter names the format, the file name's extension is not used:

```csharp
project.WriteToFile(@"C:\data\bracket.cdb.json");
project.Export(@"C:\data\bracket.step", "step"); // the active model: solids, shells and faces as BREP, curves as curve sets
project.Export(@"C:\data\bracket.dxf", "dxf");   // all models; DWG with "dwg"
project.Export(@"C:\data\bracket.stl", "stl");   // the triangulation of the active model's faces
```

`Export` returns false for a format it cannot write (e.g. IGES). The DXF version and the STL precision
are global settings, `DxfDwg.Version` and `Export.STL.Precision`:

```csharp
Settings.GlobalSettings.SetValue("Export.STL.Precision", 0.001);
```

## Show the model in a Windows.Forms application

CADability.Forms contains [CadControl](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadControl.html),
a UserControl with the 3d view (OpenGL), the properties explorer and the toolbars, and
[CadForm](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadForm.html),
a complete main window with the menu, which `CADability.App` (the application in the repository) derives
from. To put the CAD view into your own form, add a `CadControl` and give it a project once the form is
loaded (its frame is created with the window handle, so not in the constructor):

```csharp
using CADability;
using CADability.Forms;
using System.Windows.Forms;

public class MainForm : Form
{
    private readonly CadControl cadControl = new CadControl();

    public MainForm()
    {
        Text = "My CAD";
        Size = new System.Drawing.Size(1200, 800);
        cadControl.Dock = DockStyle.Fill;
        cadControl.CreateMainMenu = true; // CADability's menu (file, edit, construct...) on this form
        cadControl.CommandRequest = menuId => // handle or veto menu commands before CADability does
        {
            if (menuId == "MenuId.App.Exit") { Close(); return true; }
            return false;
        };
        Controls.Add(cadControl);
    }

    protected override void OnLoad(System.EventArgs e)
    {
        base.OnLoad(e);
        Project project = Project.ReadFromFile(@"C:\data\bracket.stp", "step");
        if (project != null) cadControl.CadFrame.Project = project;
        else cadControl.CadFrame.GenerateNewProject();
    }
}
```

`cadControl.CadFrame` is the [IFrame](xref:CADability.IFrame): it holds the project and the active view,
starts the actions (`SetAction`) and gives access to the user interface (`UIService`). Objects you add to
the model with `Add` appear in the view immediately. The [organizational classes](orgclass.md) article
describes the frame, the views and the menus.

## Where to go from here

- The articles on the [data model](database.md) and the [boundary representation](brep.md).
- The test projects in the repository (`tests/CADability.Tests`) build solids, run boolean operations and
  import and export files; they are a source of working examples.
- Settings that influence the imports and exports, e.g. `ImportStep.UseHierarchy`, `StepImport.Parallel`,
  `DxfDwg.ImportDimension`, are read from [Settings.GlobalSettings](xref:CADability.Settings.GlobalSettings)
  and can be set from code or in the settings of the user interface.
