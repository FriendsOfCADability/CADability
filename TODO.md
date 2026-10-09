# Issue triage — open items

Result of going through all open issues on 2026-10-09 against master
`17f949e`. Issue #173 was skipped because it is already being worked on.

Closed during this triage, because they were already solved on master:

- #63 Parallel STEP import: merged in 64b5e4eb and enabled by default through
  `StepImport.Parallel`.
- #238 MText support: fixed by the ACadSharp migration (#315) and by b7bb425d
  and 9573342d.
- #295 Make3D.MakePipe with a straight path: fixed in #307 (a75ce094).

Fixed during this triage (#369): #251 (dead `objectPointSav` code removed
from ToolsRoundIn) and #294 (README links and the repository website now
point to friendsofcadability.github.io).

Fixed in the follow-up pull request: #347 (`NurbsSurface.GetPars` no longer
loops forever on tiny domains), #287 (hatch polyline boundaries are always
closed on DXF import), #204 and #147 (`Plane.FromPoints` fits relative to the
centroid and orients the normal by a fixed rule).

Fixed in the next pull request: #194 (`Border.UnsplittedOutline` survives
`CompoundShape.CreateFromList`), #293 (DXF block contents on layer 0 or
ByBlock take the layer and colour of the INSERT; `CDfromParent` children keep
the block colour after cloning and loading) and #66 (STEP export writes a
`Path` edge curve, or any curve without its own STEP entity, as a B-spline).
The reporters' files are now regression test data. Follow-up found while
fixing #66: for a `CLOSED_SHELL` with a single open face, `Shell.CloseEdgeLoop`
adds a coincident face with the same normal (ImportStep repair heuristic).

Closed as not reproducible: #265 (the STEP round trip gives an identical solid
on master and on the commit from the time of the report, 0fb61554).

Closed as stale: #185 (designer exception from the net48 era; CADability.Forms
targets net8.0-windows since 517d643f, untested in the designer).

Closed as not planned: #192 (the DebuggerVisualizers project was removed in
d3db7052; a rewrite is not planned, prototype on branch `ShapeItProgress`).

## Could not be verified / unsure

These need someone with the original files, Windows/Visual Studio, or a
maintainer decision.

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
## Confirmed — still present on master

Each of these was reproduced, or confirmed by reading the code, on `17f949e`.

- [ ] **#168 Solid.Subtract returns null on large objects.** Reproduced with
  the reporter's `Bug.zip` (`project.json`, solids `main53335` and
  `cut53335`). Both inputs are closed and consistent: `main` has 82 faces
  (72 cylindrical, 10 planar), `cut` is a 10-face box, and they overlap.
  `Subtract(cut, main)`, `Subtract(main, cut)` and `Intersect(cut, main)` all
  return an empty array, and a debug build fires `Debug.Assert(fc.CheckConsistency())`
  in `BRepOperation.Result()` (`BRepIntersection.cs`). The size is not the
  cause: the result is the same after moving the pair to the origin and after
  scaling by 0.1 or 10. Subtracting a simple box from either solid works. The
  likely trigger is near-coincident faces: the cut's faces lie about 3e-7 to
  2.5e-3 from main's faces (x 500.9975 vs 501, z 941.9999997 vs 942). Add the
  file as a test case under `tests/CADability.Tests/Files` and debug
  `BRepOperation`.

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

## Documentation

- [ ] The generated documentation is stale: `docs/CADabilityDoc` was last
  generated on 2021-05-31 and `docs/index.md` is the unchanged Jekyll
  template. Consider running docfx (`CADability/docfx.json`,
  `CADability.Forms/docfx.json`) in the Pages workflow instead of committing
  generated HTML.

## Open discussions / feature requests (no action from triage)

- #256 CADability 2.0 ideas: an ongoing discussion. NuGet package and
  removal of PowerCollections (ebf1007f) are done.
- #367 Progress bar during file open: being worked on by davidebazzi
  (cc76c2e and review comments).
