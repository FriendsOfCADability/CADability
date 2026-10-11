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

Fixed by porting branch `claude/ecstatic-fermat-uml5l3`: #167 (a DXF
DIMENSION without its block is drawn instead of dropped; CADability
dimensions are exported, as a block by default or as DIMENSION entities;
optional import as CADability `Dimension` objects; both chosen by the
existing settings `DxfDwg.ExportDimension` and `DxfDwg.ImportDimension`, and
the DXF version by `DxfDwg.Version`).

Fixed during this triage (#369): #251 (dead `objectPointSav` code removed
from ToolsRoundIn) and #294 (README links and the repository website now
point to friendsofcadability.github.io).

Fixed in the follow-up pull request: #347 (`NurbsSurface.GetPars` no longer
loops forever on tiny domains), #287 (hatch polyline boundaries are always
closed on DXF import), #204 and #147 (`Plane.FromPoints` fits relative to the
centroid and orients the normal by a fixed rule).

Fixed later: #308 (`ActionFeedBack.Repaint` paints the feedback once per view
call into one display list), #253 (opt-in `CapturesMouse` on
`BooleanInput` and `MultipleChoiceInput`; new public API, so the next release
is a minor one), #254 (`MultipleChoiceInput.SetChoices`, also new public API),
#249 (an approximated curve keeps its colour), #65 (changing the colour or
another attribute of an edge is reported by its solid, shell or face, so the
view is repainted and the change can be undone), #303 (every `Model.Add`
overload adds invalid objects; construct actions and the DXF import check
`HasValidData` themselves, and the DXF and STEP exports skip invalid
objects).

Fixed from the list below: `ImportSTL.Read` closes the file,
`Border.UnsplittedOutline` is saved, an empty `Polyline` can be cloned, moved,
reversed and saved (the JSON reader accepts a property written as null), an
arc without sweep can be approximated, `Project.Export` returns whether it
wrote the file, and `Shell.OpenEdgesExceptPoles` no longer takes a closed
curve for a pole (a STEP `CLOSED_SHELL` with a single disc became a solid).
Two spheres intersect in an exact circle: the general intersection
approximated it, and the union of two spheres of radius 5 whose centres are
6 apart did not return.

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

## Confirmed — still present on master

Each of these was reproduced, or confirmed by reading the code, on `17f949e`.
All issues that could not be verified at first have since been checked.

None left: all of them have been fixed.

## Found while fixing (not yet addressed)

- [ ] Boolean operations still fail in some near-coincident configurations
  (a sweep of 267 box/cylinder/sphere/fillet configurations for #168: 71
  remain wrong, 2 crash with a stack overflow or a timeout, all unchanged by
  the fix). Examples: a box bottom 3e-7 below the tangent plane of a rounded
  edge, gaps of about 1e-5 (between `Precision.eps` and the BRep precision),
  touching boxes (`Intersect` throws), and tools 1e6 times smaller than the
  other solid. A box minus a sphere of radius 4 centred on its top face fails
  an assertion in Debug builds and returns the box unchanged in Release
  builds. Two spheres now intersect in an exact circle. Still wrong: a sphere
  of radius 5 at the origin and one of radius 5 at (6, 0, 0.5), both with
  their axes along z, give empty results, as before. The edges of both
  spheres cross exactly on the intersection circle, so every intersection
  point is found four times, and no intersection edge is made. Unions of
  spheres with offset axes measure 0.03 to 0.05 % too small or too large,
  while both differences are exact. In the union, the hole edges of the
  merged faces have `Path2D` curves, and these faces triangulate to a slightly
  smaller area than the same faces in the differences; not yet investigated
  further.

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
- [ ] CADability has no "floating" layer 0 in blocks. The DXF import
  resolves block contents on layer 0 to the layer of the INSERT (#293), so
  they are displayed and hidden like in AutoCAD, but the information is lost:
  moving the block to another layer later leaves those contents on the old
  layer, and the DXF export writes them on that layer instead of layer 0. A
  complete solution needs a "layer from parent" counterpart to `CDfromParent`.
- [ ] DXF hatch line styles are not told apart by dash pattern, pattern hatch
  lines keep the style's lineweight (no ByBlock), and a top-level ByBlock
  linetype still maps to the solid "ByBlock" pattern.

- [ ] The settings `DxfDwg.Format` (DWG/DXF/DXB) and `DxfDwg.Text`
  (Fit/Original) are shown in the control center but read nowhere. Either
  implement them (ACadSharp can write DWG) or remove them from `Settings.cs`
  and the string tables. `Project.Export` now returns false for "dwg" and
  "dxb", which it does not write.
- [ ] The export dialog offers HTML, but `Project.Export` writes nothing for
  "html". `Project.ExportToWebGl` exists, but builds the path of its template
  `WebGLS.html` with backslashes and the template is not part of the package.

- [ ] A `Polyline` without vertices still throws in the curve operations
  that need a point (`StartPoint`, `PointAt`, `PositionOf`, `Length`, ...).
  Cloning, moving, reversing and saving it work now.

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
