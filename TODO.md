# Issue triage — open items

Result of going through all open issues on 2026-10-09 against master
`17f949e`. Issue #173 was skipped because it was already being worked on; it
is fixed in #374, see below.

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
A sphere and a plane intersect in the parts of a circle that lie on each
face of the sphere: the whole circle was returned for every face, so a box
minus a sphere returned the box unchanged. The same holds for a sphere and a
cone, cylinder or torus on an axis through its centre (a cylinder first
returned every circle on every face of the sphere, a cone divided by a null
vector). Two spheres intersect in an exact circle: the general intersection
approximated it, and the union of two spheres of radius 5 whose centres are
6 apart did not return. Spheres side by side work as well: where the seams of
both spheres cross on the intersection circle, the parts of the seams between
the crossing points were taken for an edge both faces already have, and a
circle that crosses no edge was dropped, because the exact intersection
returns it in two halves. Solids that touch without a common volume give
correct results: `Solid.Intersect` threw a `NullReferenceException` whenever
they touched in a face, solids touching in an edge or a point had a whole box
as intersection, and two boxes touching in a whole face had empty
differences. DXF pattern hatches: line hatch styles are told apart by their
dashes, which become positive dash and gap lengths in drawing units (the
negative gaps of DXF were kept, and the patterns were not added to the
project), the lines are as far apart as the perpendicular part of the pattern
offset, and in a block they take the lineweight of the INSERT for ByBlock and
of its layer for ByLayer on layer "0". The linetype ByBlock outside of blocks
is "Continuous", and the project gets no line patterns "ByLayer" and
"ByBlock". A cylinder standing on a box, its bottom circle touching the sides
of the top face, is no longer lost in the union: where an intersection curve
touches the outline of a face, the loop takes the edge that turns most to the
left, and the top face is split into its four corners instead of getting a
hole that touches the outline. A cylinder lying on a box no longer has empty
differences: a tangential intersection curve along which the surfaces only
touch, without crossing, is no intersection edge.

The documentation is generated again: the package contains the XML
documentation (`CADability.xml`), and the Pages workflow builds the libraries
and runs docfx on them, under the old paths `/CADabilityDoc` and
`/CADabilityFormsDoc`. The generated HTML of 2021 is no longer in the
repository, and `docs/index.html` is a start page instead of the Jekyll
template.

A `PrecisionOverride` applies only to the thread that creates it.
`Border.Clip` overrides `Precision.eps` for every call, and every other thread
computed with that precision in the meantime, e.g. in the parallel STEP import
and in parallel tests. `Precision.eps` and `Precision.epsa` are properties now
instead of fields: source code compiles unchanged, but binaries compiled
against 1.5.0 or earlier that read them have to be recompiled (release notes
of 1.6.0).

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

Fixed in #374: #173 (splines with coinciding poles, the usual way to model a
corner). `BSpline.GetProjectedCurve` returns the exact projection instead of a
spline through some points that rounded the corners, `BSpline2D` handles
points where the derivative vanishes, and the DXF import keeps such a spline
as one `BSpline` instead of a polyline, so the export writes it back with the
same poles and knots. Extrude, Rotate, MakePipe and faces made from curves
split the spline at its corners, and these faces keep splines and ellipses
exact instead of approximating them by thousands of arcs.

Fixed from the list below: the export dialog writes DWG through ACadSharp
and no longer offers HTML, which nothing wrote; the settings `DxfDwg.Format`
and `DxfDwg.Text`, which were read nowhere, are removed.

Fixed in the review before 1.6.0: #373 (XData points are `GeoPoint`s,
displacements and directions `GeoVector`s and layer references the name of
the layer, so no ACadSharp type is in `ExtendedEntityData.Data`; points,
binary data and layer names survive a cdb file; the DXF export writes points
as points, merges the X/Y/Z triples of the netDxf era into one point and
takes a database handle as the `ulong` the import gives). Also: a helix from a
file of 1.5.0 or earlier is read with an empty `UserData` instead of null
(`HelicalCurve` did not write its base attributes before, and
`IGeoObjectImpl.SetObjectData` took the missing entry for null);
`Make3D.Rotate` of a face whose edge is a spline with corners no longer runs
out of the face's edges (`IndexOutOfRangeException`), the path of a face is
not split; a spline in Bézier form (every inner knot with the multiplicity of
the degree) is split only at real corners, where the tangents differ or the
derivative vanishes on one side, so extruding or rotating it or making a face
from it gives one face per smooth piece and not one per segment; the
intersection circle of a sphere is split at the seam wherever the domain of
the face lies (the seam was looked for at the natural bounds, which are the
used area of a face or unbounded, so a face straddling the seam got a part
that jumped by the period); `Shell.CloseOpenEdges` no longer dereferences the
curve of a pole edge; `IGeoObjectImpl.UniqueIdCounter` is incremented with
`Interlocked`, like the hash code counters of #389.

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
  and tools 1e6 times smaller than the other solid. Solids touching only in
  an edge, a line or a point have no union (`Solid.Unite` returns null, the
  result would not be a manifold), and a solid touching the inside of another
  one, where the shells do not cross, cannot be subtracted (the result would
  have a cavity touching the hull; the other solid is returned unchanged). A
  solid inside another one without touching it becomes a cavity (#416), but
  the boolean operations take only the outer shell of a solid into account.
  The unions of spheres with offset axes, which measured 0.03 to 0.05 % off,
  are exact since the sphere fixes of #525 to #528 (3.7e-6 for the
  triangulation at 1e-4, below 1e-6 integrated); `SphereBooleanTests` checks
  them to 1e-5 now instead of 2e-3.
- [x] A cube of 10 and a sphere whose axis lies in a face of the cube had
  neither a union nor a difference: the sphere of radius 3 centred on the
  middle of an edge, (10, 5, 10), and the sphere of radius 4 centred on a
  corner, (10, 10, 10). Fixed by the exact `HitTest(BoundingCube)` of #521:
  the old one of `SphericalSurface` missed slabs through the sphere and lost
  faces in the octtree. With the axis tilted in the face, the sphere on the
  middle of the face still failed (a pole widened the bounds of a half sphere
  in `Surfaces.Intersect`), fixed in `claude/sphere-boolean-axis-in-face`.
- [x] The union of the cube with a sphere centred on a corner, whose axis is
  tilted in the face x = 10, gave only the sphere: the edge of the cube along
  x crosses the seam of the sphere exactly, at (10 - r, 10, 10), and the
  intersection curves on one half sphere form a loop that touches its outline
  there. Fixed in `claude/boolean-edge-crosses-seam` (`MergeTouchingHole`).
- [x] The difference of the cube and a sphere centred on the middle of an
  edge, tilted by 1.2 about x, was 0.08 % too small: `CombineConnectedFaces`
  merged the two parts of the sphere across its seam, and the pole edge of
  one part shrank to a point. Faces with a pole edge are no longer combined.
- [x] The union of the cube with a sphere on a corner, rotated by 0.7 about
  (1, 2, 3), was 1e-4 too large: `Face.ReverseOrientation` kept the arc
  approximation of a 2d curve close to the pole of the sphere, 0.16 off its
  edge, because it compared the exact curve with that approximation.
- [x] `ShellMetrics.IntegratedVolume` was wrong for sphere faces bounded at
  a pole: by 2.8 for the difference of the cube and a sphere of radius 3 on
  an edge, rotated by 1.2 about x. `Face.Area` joined the 2d curves at the
  pole by a line across the face; fixed in #528 (`ConnectAtSingularities`).
- [x] A face triangulation stops refining at 100000 triangles, in the middle
  of a round of edge splits. A precision that needs more (1e-6 on a sphere of
  radius 3) gave a worse mesh than a coarser one, and since a face kept its
  finest triangulation, `Solid.Volume` depended on which precision was asked
  before. Fixed in #527: an incomplete triangulation is no longer reused for a
  coarser precision, and `Shell.Volume` retriangulates coarser while a face is
  incomplete. The cap itself stays.

- [x] `Face.MakeRegularSurface` did not simplify exact rational NURBS
  cylinders, cones, spheres and tori, because it maps the edges with the
  reparametrisation of `NurbsSurface.GetSimpleSurface`, which cannot follow a
  rational parametrisation. It now recomputes the 2d curves from the 3D
  curves of the edges, as `Shell.MakeRegularSurfaces` does, and puts a pole
  edge on the singular line; faces with a seam are still left as they are.
- [ ] The meshes of neighbouring faces do not always meet exactly after a
  boolean operation with near-coincident faces, so the volume of the result
  depends slightly on the point the tetrahedra are spanned from (up to 2e-8
  relative in the boolean sweep). The cause is the geometry, not the
  triangulation: when two faces are taken as coincident within the
  precision (a box 1e-5 beside a face of a long bar), the merged face keeps
  vertices 1e-5 off its plane, and the 2d curves of a neighbouring face
  still end where the vertex was before. Moving the mesh points onto the
  vertices closes the gaps, but tilts the long triangles of the big face:
  the volume of bar - box became 0.11 off instead of 0.0085. Projecting the
  merged vertices onto the coincident faces in `BRepOperation` would be the
  fix.
- [ ] CADability has no "floating" layer 0 in blocks. The DXF import
  resolves block contents on layer 0 to the layer of the INSERT (#293), so
  they are displayed and hidden like in AutoCAD, but the information is lost:
  moving the block to another layer later leaves those contents on the old
  layer, and the DXF export writes them on that layer instead of layer 0. A
  complete solution needs a "layer from parent" counterpart to `CDfromParent`.
- [ ] A DXF SPLINE comes back without the periodic flag when its knots are
  clamped (the issue 173 file has flags 11, the export writes 9), and with the
  default knot and control point tolerances (42, 43). Neither follows from the
  curve, and `BSpline` has no place to keep them.
- [ ] `BSpline.IsClosed` means periodic, so a closed spline with clamped knots
  (start point equal to end point) counts as open.
- [ ] The triangulation of a ruled surface between two splines
  (`Make3D.MakeRuledShell`) overestimates its area by up to 3.7 % at a
  precision of a thousandth of its size: long thin triangles along the rulings
  overlap their neighbours while their edges stay within the deflection.
  `Triangulation.FlipToDelaunay` leaves them, because a ruling is exact and the
  other diagonal is not. Measured again against the integrated area: 2.9 %
  too large at 1e-3 for an ellipse ruled to a turned circle, and 14 to 71 %
  at 1e-2 to 1e-3 for a flat band (height 5, radius 50) twisted by 1.2 rad,
  whose height is within the inner deflection (5 times the precision), so
  any triangle passes. Comparing the area of each triangle with the area of
  the surface over its parameter triangle catches them, but does not help:
  the triangles at the border cannot get better than their border edge,
  which is never split, so splitting their other edges only made slivers
  (up to 214000 triangles instead of 1000), and without them nothing
  changed at coarse precisions. The cause is the border: the points on the
  spline edges follow the curvature of the curves, not the twist of the
  rulings between them. A fix needs the edges of ruled faces sampled by the
  twist (the same points for both faces of an edge) or a structured
  triangulation along the rulings.
  The same geometry as a NURBS of degree (3, 1), the form in which STEP files
  bring ruled faces, triangulates with exactly the same errors, so the
  problem is not limited to `Make3D.MakeRuledShell`. Developable faces, the
  only ones a sheet metal part can have, are hardly affected (0.1 to 0.3 %
  for a transition with little twist, cones are fine), and an unfolding would
  be computed from the exact surfaces anyway, not from the triangulation.
  ShapeIt's `CDTriangulation` fixes the ruled faces (0.06 to 0.3 % at 1e-3),
  but as a drop-in replacement in master (branch
  `claude/cdtriangulation-trial`) a sphere became 9.2 % too large at 1e-3,
  13 kernel tests failed and one hung. Deferred until it is decided whether
  the kernels of CADability and ShapeIt are merged.
- [ ] The DXF writer of ACadSharp writes every handle reference of XData,
  a layer reference (1003) included, as a database handle (1005), see
  `DxfSectionWriterBase.writeExtendedData`. The export therefore writes a
  layer name as the handle of the layer, which comes back as a handle, not
  as a name (#373 left this as it is). A database handle (1005) refers to
  the document the XData came from; ACadSharp writes 0 when no object of the
  exported document has it.

## ShapeIt fork: changes not yet ported

The kernel in the ShapeIt repository (FriendsOfCADability/ShapeIt, branch
`develop`) was compared with master on 2026-11-04 (ShapeIt 3319b34, master
ad7062c4). The small fixes up to then are ported in
`claude/shapeit-port-geometry-fixes`. Left for later:

- [ ] Worth porting with some adaptation: typed lists and arrays when reading
  JSON (a4adab2), inflection points of `BSpline2D` as polynomial roots
  (9d8fcd0, keep the exclusion of corner knots), touching points in
  `GeneralCurve2D.Intersect` (8a54652) and snapping imprecise curve/surface
  intersections to the curve ends (fbdad96); the last two change where
  intersection points are made and need the boolean sweep. ShapeIt keeps the
  feature names of faces (`Shell.AddFeature`) when a shell is cloned or
  saved; whether master loses them was not checked yet.
- [ ] Larger self-contained parts: `Shell.PushPull` (with its tests), the
  formula evaluator of `FormulaInput.cs` for length, angle and point
  properties, `BVH`, `StlSurfaceReconstruction` (analytic surfaces from STL).
- [ ] Not portable on their own: fillets and chamfers of solid edges
  (`BlendEdges`, `RoundEdges`, `ChamferEdges`, `PipeContactCurve`,
  `SweptCircleSurface`; master's `BRepOperation.ChamferEdges` always returns
  null), `MakeHollow` and the parallel edge/face intersections of the boolean
  operation. (`CDTriangulation` has the interface of `Triangulation` and only
  uses `ISurface`, but it needs more than the class itself, see the ruled
  faces above.) They depend on ShapeIt's rewritten
  `BooleanOperation`, `ISurface.Domain` and `ParallelepipedHull`, about 18,000
  lines; this needs a decision whether the kernels are merged.

## Documentation

- [x] The about 440 broken doc comments (wrong `param` names, unresolved
  `cref`s, malformed XML) are fixed and the warnings CS1570 to CS1587 and
  CS1734 are no longer suppressed; only CS1591 (no doc comment) stays in
  `NoWarn`. There is a "Getting started" article with code for the main
  entry points, and `Make3D`, the boolean operations of `Solid`, the import
  and export methods of `Project`, `Model.Add` and the STEP and STL classes
  are documented (#403).
- [x] Content, see #403: the 116 empty and the 136 German summaries are
  written and translated, the public members that only had `param` tags have
  a summary, and the about 1,200 "Overrides ..." / "Implements ..." summaries
  are `<inheritdoc/>` now, so the pages of the overrides show the description
  of the base member (docfx resolves it, also over two levels; the base
  members in `IGeoObject`, `IGeoObjectImpl`, `ISurface`, `IShowProperty` and
  `IPropertyEntry` were documented for that). The 76 `GetObjectData`
  implementations say what the binary serialization is still for.
- [x] Coverage, see #403: the GeoObject types, the attributes and their
  lists, `Model`, `Project`, the views, the geometry basics, the surfaces,
  the 2d curves and shapes, the import and export classes, the property
  classes and the actions are documented, the about 900 overrides without a
  comment are `<inheritdoc/>`, and every public type has a summary. Of the
  about 14,500 public types and members docfx lists, about 850 (6 %) have no
  doc comment now, down from about 9,100 (63 %). They are members of
  internal helpers (`Polynom`, `BRepIntersection`, `QuadTree`,
  `JsonSerialize`, `ExtendedEntityData`...), so CS1591 stays in `NoWarn`;
  the about 4,400 OpenGL and Win32 constants in `OpenGL.cs` and
  `FormsSubst.cs` mirror the C headers and suppress CS1591 with a pragma.
  CADability.Forms still has 139 members without a comment.

## Open discussions / feature requests (no action from triage)

- #256 CADability 2.0 ideas: an ongoing discussion. NuGet package and
  removal of PowerCollections (ebf1007f) are done.
- #367 Progress bar during file open: being worked on by davidebazzi
  (cc76c2e and review comments).
