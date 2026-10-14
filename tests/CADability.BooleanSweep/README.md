# Boolean sweep

A console tool that runs the boolean operations of pairs of solids in near-coincident positions and compares the volumes of
the results with the exact ones (issue #400). It rebuilds the sweep made for #168, which was not part of the repository.

It is not a test project: `dotnet test` does not run it, and it is not part of `CADability.sln`. A full run takes about two
minutes on four cores.

## Running it

Build it in **Release**. In a Debug build a failing `Debug.Assert` in CADability ends the process, and the case is reported as
a crash.

```
dotnet build tests/CADability.BooleanSweep -c Release
dotnet tests/CADability.BooleanSweep/bin/Release/net8.0/CADability.BooleanSweep.dll --out sweep-results --baseline tests/CADability.BooleanSweep/baseline.csv
```

Options:

| option | meaning |
|---|---|
| `--out <dir>` | where `sweep.csv` and `summary.md` are written (default `sweep-results`) |
| `--baseline <csv>` | an earlier `sweep.csv`; the summary lists the cases that were fixed or broken since |
| `--filter <text>` | only the cases whose id contains the text, e.g. `fillet-bar-box` or `/Intersect` |
| `--parallel <n>` | number of cases run at the same time (default: number of processors) |
| `--timeout <s>` | seconds after which a case is stopped and reported as `TIMEOUT` (default 120) |

`list` prints the ids of all cases, `run <id>` runs one case in the current process, which is the way to debug it.

## The cases

17 variants of a pair of solids A and B, each at 11 offsets d and with 4 operations (A − B, B − A, A ∩ B, A ∪ B): 748 cases.
d < 0 means that the solids reach into each other by |d|, d > 0 a gap, d = 0 contact. The offsets are ±2.5e-3, ±1e-5,
±1e-6, ±3e-7, ±1e-7 and 0. The precision of an operation is 1e-6 of the size of the smaller solid, about 1e-5 here, so the
small offsets lie at and below it.

- box/box: a box on the top face of a cube, inside the face, over an edge, and with flush sides
- a box near the face of a bar 3000 long (issue #168)
- cylinder/box: lying on the box, standing on it, and inside it on its bottom
- holes: a blind hole whose bottom is near the top face, and a through hole whose ends are near both faces
- cylinder/cylinder: parallel axes d apart, the same axis with the radii 5 and 5 + d, and crossing axes with the radii 5 and
  5 + d
- sphere/box: on the box and inside it on its bottom
- a bar with a rounded edge and a box near the plane the rounding is tangent to (issue #168): covering the rounding,
  starting on it, and tilted by 3e-8

`Cases.cs` gives the exact volumes of A, B and A ∩ B as functions of d, partly by numeric integration.

## Statuses

| status | meaning |
|---|---|
| `OK` | the volume of the result is the exact one within the tolerance, and the result has no open edges |
| `VOLUME` | the volume is wrong |
| `EMPTY` | no result, although the exact one is not empty |
| `OPEN` | the result has open edges |
| `EXCEPTION`, `TIMEOUT`, `CRASH` | the operation threw, did not finish, or ended the process |
| `LIMITATION` | a documented limitation, not a failure: `Solid.Unite` returns null for disjoint solids and for solids touching only in a line or a point, and a solid touching the inside of another one cannot be subtracted (the first one is returned unchanged). Offsets up to 1e-5 count as touching here too. |

The tolerance is 1e-7 of the larger operand plus 1e-6 of the result. For offsets up to 1e-5 the result at d = 0 is accepted as
well, i.e. solids that are that close may be taken as touching.

The volume is that of a fine triangulation, `Solid.Volume(1e-4)`. `ShellMetrics.IntegratedVolume` is written to the CSV too
(column `integrated`) for comparison. On faces bounded by intersection curves it was up to 1e-4 relative off until #459;
on the correct results of the crossing cylinders both now agree to about 1e-9. A large difference between the two columns
shows a broken result, where some faces of it cannot be integrated and fall back to their triangles.

## baseline.csv

The latest result: 691 OK, 16 VOLUME, 4 EMPTY, 37 LIMITATION, no exceptions, timeouts or crashes. All failures are those of
the crossing cylinders of (almost) equal radius (#458), at offsets of 1e-5 and below, and one at 0 (their union).

History:
- `master` at a96f42bd (14 October 2026): 594 OK, 58 VOLUME, 63 EMPTY, 33 LIMITATION.
- Touching solids taken for nested ones, and edges ending within the precision of a face that did not intersect it: 45
  cases fixed, none broken.
- A box within the precision of the plane a rounded edge is tangent to, not split along the tangent edge: 14 cases fixed,
  none broken.
- Cylinders whose radii or axes differ by less than the precision not taken as the same surface: 18 cases fixed, none
  broken.
- Grazing contacts below the precision (#457): grazing intersection points of an edge dropped where the edge stays within
  the precision of the other shell, tangential contacts at edge ends and at borders of overlapping faces added, open edges
  connected within the precision of the operation: 20 cases fixed, none broken.

Replace it with the `sweep.csv` of a newer run when a change fixes cases, so the next comparison starts from there.
