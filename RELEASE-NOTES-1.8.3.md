# CADability 1.8.3

CADability 1.8.3 is on [nuget.org](https://www.nuget.org/packages/CADability/1.8.3). Patch release with a fix for absurd areas of non-rational splines.

> **No recompilation needed.** 1.8.3 is source and binary compatible with 1.8.2: no public API is added, removed or changed.

## Fixes

- **`BSpline2D.GetArea`**: the area of a non-rational spline could be wrong by any amount when the spline has a short knot span far from parameter 0. The closed-form area came from `ExplicitPCurve2D`, whose polynomials are written in the global knot parameter; on a short span their coefficients grow with the inverse power of the span width to the degree and cancel each other only in exact arithmetic. A quintic with a last span of width 1e-3 lost 1.5 percent of its area, one with a span of width 5e-7 at its end measured around 1e33. The plausibility check against the chord polygon through the knots did not catch it, because that polygon was evaluated through the same polynomials and came out just as wrong. Such splines occur in practice: reversing a spline whose FIRST span is short, which is harmless, moves that span to the end, and `CompoundShape.CreateFromList` reverses segments while it assembles contours. A DXF part of 475 x 86 mm got a hole of -4.8e33 mm^2 and thus a `SimpleShape.Area` of 4.8e33. `GetArea` now always takes the Gauss-Legendre integration that rational splines already used. It evaluates the NURBS itself, is exact for polynomial curves up to degree 7 and converges for higher degrees, and is not slower than the closed form was.

## Behaviour changes

- `BSpline2D.GetArea` of a non-rational spline may differ from 1.8.2 in the last digits even where 1.8.2 was right, because it is integrated now instead of taken in closed form. `Border.Area`, `SimpleShape.Area` and `CompoundShape.Area` sum it and follow.

## Package

`CADability.1.8.3.nupkg` contains `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded, as in 1.8.2. Dependencies are unchanged from 1.8.2. Symbols are on the nuget.org symbol server (`CADability.1.8.3.snupkg`, Source Link).
