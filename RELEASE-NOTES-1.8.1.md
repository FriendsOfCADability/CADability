# CADability 1.8.1

CADability 1.8.1 is on [nuget.org](https://www.nuget.org/packages/CADability/1.8.1). Patch release with fixes in the approximation of 2d paths and in distances to flat ellipses.

> **No recompilation needed.** 1.8.1 is source and binary compatible with 1.8.0: no public API is added, removed or changed. `Ellipse2D` now overrides `PerpendicularFoot`, which it inherited before; callers are not affected.

## Fixed issues

#554 `Path2D.Approximate` cut off arcs when the path started with an arc · #556 `EllipseArc2D.MinDistance` overestimated the distance on flat ellipses · #557 `Ellipse2D.PerpendicularFoot` returned points that are not foot points on flat ellipses.

## Fixes

- **`Path2D.Approximate`** (#555):
  - Whether a path is approximated segment by segment or by subdivision no longer depends on its start segment. A path that started with an arc (with `linesOnly`) went into the subdivision meant for chains of short tangential curves, where a chord was accepted as soon as its midpoint was close to any part of the whole path. A quarter ring 70/100 starting with its outer arc lost that arc: 1176 instead of 4006 mm². The 3d `Path.Approximate` and the *Approximate* command pass whole paths to it.
  - The subdivision measures chords and arcs against the part of the path between the two parameters only, not against the whole path.
- **Flat ellipses** (#558): foot points of ellipses and elliptical arcs are computed exactly in the frame of the axes instead of from the triangulation. `Ellipse2D.PerpendicularFoot` and `EllipseArc2D.MinDistance` are now correct for very flat ellipses as well; `MinDistance` of an arc takes the minimum over its end points and all local minima inside it.

## Behaviour changes

- **`Approximate(false, …)` returns arcs** for chains of tangential curves (#555). With arcs allowed, the subdivision never produced arcs before, because a single line from `Curves2D.ConnectByTwoArcs` was taken for "arcs too small" and the whole part fell back to lines. An ellipse made of 360 elliptical arcs now becomes 32 arcs instead of 64 lines at a `maxError` of 0.1, and 56 arcs instead of 216 lines at 0.01, all within the tolerance. This also applies to the *Approximate* command with the mode "lines and arcs". Objects approximated before are not changed.
- The arc check of the subdivision is faster: 75–92 ms instead of 340–560 ms for a closed chain of 20000 elliptical arcs, with unchanged results.

## Other changes

- Doc comment warnings left by `Project.WriteToFile(string, bool)` are fixed (#552).
- Three fields that only the Debug build wrote are removed; none of them was public or serialized (#553). CADability now builds without compiler warnings in Debug and Release.

## Package

`CADability.1.8.1.nupkg` contains `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded, as in 1.8.0. Dependencies are unchanged from 1.8.0. Symbols are on the nuget.org symbol server (`CADability.1.8.1.snupkg`, Source Link).
