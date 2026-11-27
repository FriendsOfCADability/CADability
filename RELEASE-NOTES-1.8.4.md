# CADability 1.8.4

> **Not published.** 1.8.4 never reached nuget.org. Its changes are first released with [1.8.5](RELEASE-NOTES-1.8.5.md).

This patch release takes over surface, curve and JSON fixes from the ShapeIt fork (FriendsOfCADability/ShapeIt, `develop` 570d479).

> **No recompilation needed.** 1.8.4 is source and binary compatible with 1.8.3: no public API is added, removed or changed.

## Fixes

- **`SurfaceOfRevolution`** (a 3d curve rotated about an axis, as `Make3D` and the STEP import create it):
  - `Orientation(GeoPoint)` threw a `NullReferenceException` for every such surface. It fell through into the code of the old implementation, which needs a 2d profile. `Intersect(ICurve, ...)` did the same for a planar curve in a plane through the axis, and for every other curve it computed the general intersection twice.
  - With a `Path` as profile, `PositionOf` was up to 0.34 off the surface and the derivatives up to 58 percent wrong. `PointAt` converted v to a position of the curve with `ICurve.ParameterToPosition`, which for a `Path` maps by length and is not the inverse of the `PositionToParameter` that `PositionOf` uses. v is now mapped linearly to the position for all curves. For lines, arcs, ellipses and BSplines this gives the values of 1.8.3, apart from an ellipse parameter outside its range, which `ParameterToPosition` wrapped by 2π. The derivatives come from `ICurve.TryPointDeriv2At` (or differences of `PointAt`), no longer from `DirectionAt`, which is only required to give the direction.
  - `SameGeometry` returned false for a copy whose curve is reversed or whose axis points the other way, and for a copy rotated about the axis. It now finds the mapping u' = ±(u − b), v' = a·v + e and checks it with points, so `Shell.CombineConnectedFaces` can merge such faces.
- **`Path.TryPointDeriv2At`** returned false, so callers had to fall back to numerical derivatives. It now returns the exact derivatives of the segment, scaled to the position of the path.
- **`RuledSurface.Derivation2At`**: the mixed derivative `duv` was always zero. It is the difference of the derivatives of the two curves, which matters for twisted ruled surfaces such as a hyperbolic paraboloid.
- **`Make3D.SewFaces`** turned open, curved shells inside out: it oriented every sewn shell "outward" with a ray test. For an open shell, a ray along the normal of a dented surface can hit the surface again, and the face was flipped. Open shells now keep the orientation of their first given face, and the other faces are oriented consistently with it. Closed shells are still oriented outward.
- **`ConicalSurface.SameGeometry`** now also recognizes a cone whose axis is reversed or has a different length (the same double cone with another v scale) and returns the mapping verified with 3d points.
- From #565, merged after 1.8.3: saving a project with objects of a type that was unknown when it was read no longer throws an `InvalidCastException` and writes the same file again (`JsonProxyType`); `SerializableHashSetString` no longer throws when its strings are missing; `Ellipse.SameGeometry` compares two arcs by their end points and the points at 1/4, 1/2 and 3/4 instead of by their ill-conditioned centers.

## Behaviour changes

- `SurfaceOfRevolution.PointAt` of a surface with a `Path` profile returns other points for the same (u, v) than in 1.8.3. In 1.8.3, `PointAt` and `PositionOf` did not agree for these surfaces, so faces built on them were already inconsistent. Surfaces with other profiles are unchanged within their parameter range.
- `Make3D.SewFaces` keeps the orientation of open shells instead of guessing it.
- `ICurve.SameGeometry` of two arcs can now return true where 1.8.3 said false, namely for short arcs whose ends differ by less than the precision.

## Package

`CADability.1.8.4.nupkg` would have contained `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded, as in 1.8.3. Dependencies are unchanged from 1.8.3. Symbols would have been on the nuget.org symbol server (`CADability.1.8.4.snupkg`, Source Link).
