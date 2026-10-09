# Issue triage — open items

Result of going through all open issues on 2026-10-09 against master
`17f949e`. Issue #173 was skipped because it is already being worked on.

Closed during this triage, because they were already solved on master:

- #63 Parallel STEP import: merged in 64b5e4eb and enabled by default through
  `StepImport.Parallel`.
- #238 MText support: fixed by the ACadSharp migration (#315) and by b7bb425d
  and 9573342d.
- #295 Make3D.MakePipe with a straight path: fixed in #307 (a75ce094).

## Could not be verified / unsure

These need someone with the original files, Windows/Visual Studio, or a
maintainer decision.

- [ ] **#168 Solid.Subtract returns null on large objects.** The `Bug.zip`
  model could not be downloaded and no matching test file exists, and no
  commit references the issue. Get the model, then run `Solid.Subtract` on
  master at the original scale and at a smaller scale.
- [ ] **#265 Exporting and importing STEP files loses data.** Does not
  reproduce: the issue's code gives the same solid (14 faces, volume
  7188520.7) after both STEP round trips, on master and on the commit from the
  time of the report (0fb61554). Ask the reporter which viewer showed the
  corruption. Possible lead: `Face.StepBound` writes hole loops as
  `FACE_OUTER_BOUND` instead of `FACE_BOUND`.
- [ ] **#185 Exception during design of CadCanvas control.** Happens only in
  the Visual Studio designer. CADability.Forms moved to net8.0-windows (#333),
  which probably removes the `Bitmap` type mismatch behind the
  `MissingMethodException`. Retest in the designer on Windows. Independently,
  `CadCanvas.Dispose(bool)` should do its OpenGL and view work only when
  `disposing` is true.
- [ ] **#192 Fix Visualizer for Visual Studio > 17.6.** The warning is gone
  because d3db7052 removed the CADability.DebuggerVisualizers project. The
  visualizers themselves don't work in current Visual Studio. A prototype
  exists on branch `ShapeItProgress` (76e05ff0). Decide whether to close the
  issue as not planned or keep it open for the rewrite.
- [ ] **#294 GitHub documentation links broken.** `README.md` still links to
  `https://sofagh.github.io/CADability/...`. github.io could not be reached
  from the sandbox, so neither the old URLs nor a FriendsOfCADability Pages
  site could be checked. Point the links at the current Pages site, or enable
  Pages for `docs/`.
- [ ] **#297 CADability WPF integration issue.** This is a usage question.
  The control is `CadControl` in namespace `CADability.Forms` (assembly
  CADability.Forms.dll), not `CADControl` in `CADability`. Draft answer: use
  `xmlns:cad="clr-namespace:CADability.Forms;assembly=CADability.Forms"`,
  target `net8.0-windows` with `UseWPF` and `UseWindowsForms`, and host the
  control in a `WindowsFormsHost`. Post the answer and close; optionally add a
  WPF hosting note to the README.
- [ ] **#165 OctTree not returning all close objects.** Works as designed:
  on master `GetObjectsCloseTo(e1)` still does not return e2, which is 9e-5
  away from e1, even after #326. `GetObjectsCloseTo` returns objects that share
  an octree leaf with the given object, so it is not a distance query. Use
  `GetObjectsFromBox` with an expanded cube, as dsn27 already answered. Answer
  stefan-tb's question and close as answered.
- [ ] **#167 Dimensions not shown in view.** Partly a question. A DIMENSION
  is imported as its anonymous block (lines, arrows, text). A DIMENSION
  without a block is dropped silently, and ByBlock colours come in black.
  Fixes exist on the unmerged branch `claude/ecstatic-fermat-uml5l3`
  (311598c3, 3bc72921, e8199e3b). Review and merge that branch, then answer
  and close.
- [ ] **#251 ToolsRoundIn.OnDone — objectPointSav is never assigned.**
  8e15ccc7 silenced the warning with an initializer, but the field is still
  always the origin, and the `roundRad == 0.0` branch in `OnDone` trims at
  the wrong point. Remove that branch, or store the real corner point. If the
  maintainers think silencing the warning is enough, close the issue
  referencing 8e15ccc7.

## Confirmed — still present on master

Each of these was reproduced, or confirmed by reading the code, on `17f949e`.

- [ ] **#347 NurbsSurface.GetSimpleSurface infinite loop.** A synthetic
  tiny-span NURBS with a singularity at umax hangs in `GetCanonicalForm` and
  `GetSimpleSurface`. The fix 9316bd2c on branch
  `347-nurbssurfacegetsimplesurface-infinite-loop` solves it, but it is not
  merged. It also rewrites the line endings of the whole file. Re-apply it as
  a clean patch, add a regression test and open a PR.
- [ ] **#66 STEP export fails for `Path` edge curves.** `Path` does not
  implement `IExportStep`, and `Edge.cs` (`IExportStep.Export`) casts without a
  null check, so the export throws `NullReferenceException`. Ruled surfaces no
  longer create `Path` edges, but older files and other code paths still can.
- [ ] **#204 Plane.FromPoints returns a -Z normal for planar curves.** All
  random point sets at z=0, and all planar splines, get normal (0,0,-1).
  `CompoundShape.CreateFromList` plus a DXF export then writes arcs with
  extrusion -Z. Make the orientation of the normal deterministic, and use a
  tolerance in the "normal ≈ ±Z" check of the `Plane` constructor.
- [ ] **#147 A spline is not rendered correctly.** This is a geometry bug,
  not an OpenGL one. A closed planar BSpline far from the origin (x ≈ 1e5)
  becomes `UnderDetermined`, because `Plane.FromPoints` fits uncentred
  coordinates, so the curve is projected onto a line. Subtract the centroid
  before the fit. This is related to #204.
- [ ] **#194 Circular hole UnsplittedOutline lost.** `Border.Reduce` assigns
  `Segments`, and the setter clears `UnsplittedOutline`, so every circle hole
  from `CompoundShape.CreateFromList` comes back with
  `UnsplittedOutline == null`.
- [ ] **#287 DXF hatch boundary not closed.** `ImportDxf.ConvertPolylineBoundary`
  drops the closing edge when group 73 is 0. A 10×10 square hatch then imports
  as a triangle with area 50. Hatch boundary polylines are always closed.
- [ ] **#293 Block colour not rendered.** ByBlock children and layer-0
  children of an INSERT come in black instead of taking the colour and layer
  of the insert. Resolve them against the INSERT, for example by using
  `ColorDef.CDfromParent`.
- [ ] **#249 Path colour changes when approximated.** `Path.Approximate`
  creates child curves with the default black colour, and `CopyAttributes`
  overwrites only null child colours, so the colour is lost.
- [ ] **#65 Edge colour not changed.** Changing an edge curve's colour is not
  forwarded to the solid, so the view is not refreshed. `Edge.PaintTo3D` also
  always paints black (8994f99d). Decide whether edge colours should be
  editable at all.
- [ ] **#308 ActionFeedBack generates too many OpenGL lists.** Still one
  list per object per repaint, an unused `foreach (IView vw in
  frame.AllViews)` loop, and the unconditional "Delete List" `Debug.WriteLine`
  in `PaintToOpenGL.cs`.
- [ ] **#253 MultipleChoiceInput & mouse move events.**
  `MultipleChoiceInput.BuildShowProperty` (and `BooleanInput`) does not
  subscribe `PropertyEntryChangedStateEvent`, so selecting it doesn't change
  the current input.
- [ ] **#254 MultipleChoiceInput with variable choices.** Not implemented
  yet. Needs new public API (`SetChoices` or a virtual property factory), which
  means a minor version bump.
- [ ] **#303 Model.Add() silently drops invalid GeoObjects.** The
  `HasValidData()` check in `Model.Add(IGeoObject)` is unchanged, and the
  other overloads still don't check. This needs a design decision.

## Open discussions / feature requests (no action from triage)

- #256 CADability 2.0 ideas: an ongoing discussion. NuGet package and
  removal of PowerCollections (ebf1007f) are done.
- #367 Progress bar during file open: being worked on by davidebazzi
  (cc76c2e and review comments).
