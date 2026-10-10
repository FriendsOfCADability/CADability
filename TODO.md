# Issue triage — open items

Result of going through all open issues on 2026-10-09 against master
`17f949e`. Issue #173 was skipped because it is already being worked on.

Closed during this triage, because they were already solved on master:

- #63 Parallel STEP import: merged in 64b5e4eb and enabled by default through
  `StepImport.Parallel`.
- #238 MText support: fixed by the ACadSharp migration (#315) and by b7bb425d
  and 9573342d.
- #295 Make3D.MakePipe with a straight path: fixed in #307 (a75ce094).

Closed as answered: #297 (WPF hosting of `CadControl`) and #165 (OctTree
`GetObjectsCloseTo` is not a distance query).

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
The reporters' files are now regression test data. The same pull request
also fixes the problems found on the way: holes are exported as `FACE_BOUND`,
paths of lines and arcs are exported as exact rational B-splines,
`Shell.CloseEdgeLoop` no longer covers a face by a coincident face, DXF block
contents also take linetype and lineweight of the INSERT, hatches and solids
take the colour of their entity, `CDfromParent` works for objects that are
already in a block, files that are only read are opened with `FileShare.Read`,
a degenerate sphere fit in `NurbsSurface.GetSimpleSurface` is detected, and
`Border.UnsplittedOutline` survives `ChangeCyclicalStart`, `Clone`,
`GetModified` and `Move`.

Closed as not reproducible: #265 (the STEP round trip gives an identical solid
on master and on the commit from the time of the report, 0fb61554).

Closed as stale: #185 (designer exception from the net48 era; CADability.Forms
targets net8.0-windows since 517d643f, untested in the designer).

Closed as not planned: #192 (the DebuggerVisualizers project was removed in
d3db7052; a rewrite is not planned, prototype on branch `ShapeItProgress`).

## Could not be verified / unsure

These need someone with the original files, Windows/Visual Studio, or a
maintainer decision.

- [ ] **#167 Dimensions not shown in view.** Answered on the issue (DXF
  dimensions are imported as their anonymous block; colours fixed in #372).
  Still open: a DIMENSION without its block (DXF R12, some exporters) is
  dropped silently. A fix that regenerates missing blocks exists on the
  unmerged branch `claude/ecstatic-fermat-uml5l3` (311598c3, together with
  3bc72921 and e8199e3b): review, port and close the issue.
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

## Found while fixing (not yet addressed)

- [ ] Boolean operations still fail in some near-coincident configurations
  (a sweep of 267 box/cylinder/sphere/fillet configurations for #168: 71
  remain wrong, 2 crash with a stack overflow or a timeout, all unchanged by
  the fix). Examples: a box bottom 3e-7 below the tangent plane of a rounded
  edge, gaps of about 1e-5 (between `Precision.eps` and the BRep precision),
  touching boxes (`Intersect` throws), and tools 1e6 times smaller than the
  other solid.

- [ ] `tests/CADability.Tests/Files/CDB/Volumes.cdb.json` cannot be read by
  the JSON reader of this repository: it stores `SphericalSurface`,
  `LayerList` and others in `IJsonSerialize` form (`$TypeVersion` 0), probably
  written by ShapeIt, but here these classes only implement `ISerializable`.
  `VolumesMatchAnalytic` and `IntegratedVolumeMatchesAnalyticOnEveryMesh` end
  in a `NullReferenceException` in `JsonSerialize.SerializationInfoFromJsonData`
  whenever the file is actually read.
- [ ] `ModOp2D.IsIsogonal` mixes matrix indices in its second check and is
  false even for the identity, so `Circle2D`/`Arc2D.GetModified` turn circles
  into `Ellipse2D` under uniform scaling. `Circle2D.GetModified` also drops the
  orientation of the circle.
- [ ] The sphere and torus branches of `NurbsSurface.GetSimpleSurface` fit an
  affine reparametrisation, which cannot follow the rational parametrisation;
  the 3D error can reach the radius. The torus fit has no degeneracy check.
- [ ] `Shell.OpenEdgesExceptPoles` treats every open edge whose start and end
  vertex coincide as a pole, including a closed B-spline edge, so a STEP
  `CLOSED_SHELL` with a single open face (issue66_fma_02.stp) still becomes a
  Solid.
- [ ] `Border.UnsplittedOutline` is not serialized and is lost on save/load.
- [ ] `ImportSTL.Read` never disposes its readers.
- [ ] CADability has no "floating" layer 0 in blocks. The DXF import
  resolves block contents on layer 0 to the layer of the INSERT (#293), so
  they are displayed and hidden like in AutoCAD, but the information is lost:
  moving the block to another layer later leaves those contents on the old
  layer, and the DXF export writes them on that layer instead of layer 0. A
  complete solution needs a "layer from parent" counterpart to `CDfromParent`.
- [ ] DXF hatch line styles are not told apart by dash pattern, pattern hatch
  lines keep the style's lineweight (no ByBlock), and a top-level ByBlock
  linetype still maps to the solid "ByBlock" pattern.

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
