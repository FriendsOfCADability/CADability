# Surface unrolling (flat pattern development)

Status: **implemented** — `CADability/SurfaceDevelopment.cs`, with
`CylindricalSurface`, `ConicalSurface` and `PlaneSurface` implementing
`IDevelopableSurface`.
Last updated: 2026-08-30

## Summary

CADability could model and import cylindrical and conical geometry but could not
develop a surface into a flat pattern. This adds an unrolling API for developable
surfaces, so that a face on a cylinder or cone — and the curves lying on it — can
be mapped into the plane.

Flat patterns are what any downstream fabrication step needs: laying out a rolled
sheet before it is bent, marking an intersection line on a tube before it is cut,
or feeding a cut contour to a machine that reaches the surface with a rotary axis.
The mathematics is small and the required building blocks were already in the
library; what was missing was the API tying them together.

## What it does

Development is an **isometry**: it maps the (u,v) parameter domain onto a flat
pattern so that every curve keeps its length. That is the property the whole thing
stands on, and it is what almost every test measures.

```csharp
public interface IDevelopableSurface
{
    bool IsDevelopable { get; }
    GeoPoint2D Develop(GeoPoint2D uv);
    GeoPoint2D DevelopInverse(GeoPoint2D flat);
}

public static class SurfaceDevelopment
{
    public static ICurve2D DevelopCurve(ISurface surface, ICurve curve, double precision);
    public static ICurve2D DevelopCurve(IDevelopableSurface surface, ICurve2D uvCurve, double precision);
    public static CompoundShape DevelopFace(Face face, double precision);
}
```

Test for the capability the way you already test for `ICylinder` or
`ISurfaceWithRadius`. A surface of the right *kind* may still refuse — see below —
so it has to be asked rather than inferred from the type.

`DevelopCurve` takes an `ISurface` rather than an `IDevelopableSurface` in the
common overload: callers hold a `Face.Surface`, and making them cast first would
buy nothing. It returns null when the surface cannot be developed.

## The mathematics

**Cylinder.** `PointAt(u,v) = toCylinder * (cos u, sin u, v)`, so u is the
circumferential angle and v the axial coordinate of the *unit* cylinder. The flat
position is `(u·r, v·|Axis|)`. The metric is diag(r², |Axis|²) and that map is an
isometry.

**Cone.** `PointAt(u,v) = toCone * (v cos u, v sin u, v)`. With radial scale `r`
and axial scale `h`, a generator advances `L = √(r² + h²)` per unit of v, so the
flat pattern is polar: `ρ = v·L`, `θ = u·r/L`. A full revolution opens only
`2π·r/L` rather than `2π` — which is exactly why a cone lies flat and a sphere
does not. Arc length checks out either way: the 3D circle at v is `2π·v·r`, and
the flat arc is `ρ·2πk = v·L·2π·r/L`, the same number.

**Plane.** `(u·|DirectionX|, v·|DirectionY|)` — the identity for the usual
unit-length axes, which is what makes callers uniform.

## Details that had to be got right

**Axial scale.** v is the coordinate of the *unit* cylinder, so the flat
coordinate is `v·|Axis|`, not `v`. A `toCylinder` carrying a non-unit axial scale
is easy to overlook and yields a pattern wrong by a constant factor with nothing
about the result looking odd. There is a test for exactly this.

**Elliptical cylinders.** `RadiusX` and `RadiusY` are independent, so
`CylindricalSurface` also represents elliptical cylinders. For those the
circumferential arc length is an incomplete elliptic integral of the second kind,
with no closed form. `IsDevelopable` returns false for them, and the circular case
does not pay for the generality. The same holds for `ConicalSurface`.

**Sheared parametrisations.** `IsDevelopable` also checks that the axes are
mutually perpendicular. A sheared `ModOp` is not a circular cylinder at all, and
the check is three dot products.

**The seam needed no handling at all — the proposal was wrong about this.**
The claim was that a curve crossing the seam comes back from `GetProjectedCurve`
with a jump of one period in u, and must be unwrapped before development. It does
not. `GetProjectedCurve` returns a *continuous* parameter curve and lets u run
outside one period where the curve does: an arc from −0.3 to +0.3 rad comes back
as 5.98 → 6.58, and a three-quarter turn from 5.0 comes back as 5.0 → 9.7. The
first implementation unwrapped anyway, anchoring each sample to the curve's start,
which folded every value past half a period backwards and doubled the length of a
full circle. Removing it was the fix. `TheSeamDoesNotChangeACurvesLength` pins the
behaviour so it stays that way.

**No seam parameter on `DevelopFace`.** A CADability face already carries an
explicit outline in (u,v), so whatever produced the face has placed the seam.
Moving it means splitting that outline against a new u value and closing it again
— a two-dimensional boolean operation, not a development. A caller who needs the
pattern cut elsewhere can rotate the surface about its axis before developing.

**Precision.** `DevelopCurve` approximates by adaptive subdivision, and the test
is made on the flat pattern rather than in parameter space — that is where the
tolerance is meant. For a cylinder `Develop` is affine, so a straight parameter
curve stays straight and no subdivision happens at all. On a cone it is polar and
subdivision earns its keep: at tolerances of 1, 0.1 and 0.01 mm a full turn comes
back with 17, 65 and 129 vertices and a worst deviation of 0.48, 0.030 and 0.0075
— comfortably inside what was asked for each time.

## Testing

`tests/CADability.Tests/SurfaceDevelopmentTests.cs`, 14 tests:

- Round trip: `DevelopInverse(Develop(uv)) ≈ uv` across the natural bounds of
  cylinder and cone (worst error 1.4e-14).
- Length preservation on both: circumference, generator, and the axial-scale case
  that catches the mistake above.
- A full cylinder develops to a rectangle of width 2πr and the face's axial
  extent, with the area to match.
- A cone band develops to an annular sector of the right area.
- A hole in the wall stays a hole in the pattern, with the right area removed.
- A curve on a cylinder keeps its length, and a circle round the tube develops to
  a straight line across the pattern.
- The same arc costs the same either side of the seam.
- Precision bounds the approximation, and a finer tolerance costs more vertices.
- Elliptical cylinders and spheres are refused rather than approximated.

A cone's development is multivalued in u — the flat pattern knows only an angle,
and u is that angle divided by a factor smaller than one. `DevelopInverse` returns
the branch matching `ConicalSurface.PositionOf`, u in [0, 2π/k), on the nappe in
front of the apex.

## Scope

In scope: cylindrical, conical and planar surfaces; curve and face development.

Out of scope: doubly curved surfaces and any approximate development of them;
`SurfaceOfLinearExtrusion`, which is developable when its extrusion direction is
constant and is the natural follow-up; bend allowance and K-factor compensation,
which are fabrication parameters rather than geometry; nesting or arranging the
resulting patterns; and re-placing the seam, as above.

## Context

This came out of planning tube-cutting support in an application built on
CADability, but the API is deliberately free of anything specific to that use, and
the capability is one a general-purpose CAD library is expected to have. Feedback
on the shape of the interface — particularly whether a capability interface or
extension methods on `ISurface` fits the library's conventions better — is
welcome.
