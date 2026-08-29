# Proposal: surface unrolling (flat pattern development)

Status: **proposal / not yet implemented**
Last updated: 2026-08-29

## Summary

CADability can model and import cylindrical and conical geometry but cannot
develop a surface into a flat pattern. This proposal adds an unrolling API for
developable surfaces, so that a face on a cylinder or cone — and the curves
lying on it — can be mapped into the plane.

Flat patterns are what any downstream fabrication step needs: laying out a
rolled sheet before it is bent, marking an intersection line on a tube before
it is cut, or feeding a cut contour to a machine that reaches the surface with
a rotary axis. The mathematics is small and the required building blocks are
already in the library; what is missing is the API that ties them together.

## Motivation

The immediate driver is tube cutting: a contour that wraps around a tube — a
hole, a mitre, a saddle joint where two tubes meet — has to be expressed as a
flat curve before it can be cut, priced, or drawn. The same operation serves
sheet-metal development of rolled and conical parts, which is a common CAD
capability that CADability currently lacks.

## What already exists

Unrolling a cylinder is a reparameterisation, and the parameterisation is
already the right one. `CylindricalSurface.PointAt`
(`CADability/CylindricalSurface.cs:182`) is

```csharp
public override GeoPoint PointAt(GeoPoint2D uv)
{
    return toCylinder * new GeoPoint(Math.Cos(uv.x), Math.Sin(uv.x), uv.y);
}
```

so `u` is the circumferential angle and `v` the axial coordinate of the unit
cylinder. The unrolled position of a point is therefore `(u · r, v · h)`, where
`r` is the circumferential radius and `h` the axial scale of `toCylinder`.
Both are already exposed:

- `RadiusX` / `RadiusY` — `(toCylinder * GeoVector.XAxis).Length` and the
  Y equivalent (`CylindricalSurface.cs:69`, `:76`)
- `Axis` — `toCylinder * GeoVector.ZAxis` (`:90`), whose length is the axial
  scale
- `Location`, `XAxis`, `YAxis` for the placement

`ISurface.GetProjectedCurve(ICurve, double)` already maps a 3D curve into
`(u, v)` space, and `IsUPeriodic` / `UPeriod` / `GetNaturalBounds`
(`CADability/Surface.cs:161`, `:171`, `:306`) describe the periodicity that
seam handling needs. `ConicalSurface.PointAt`
(`CADability/ConicalSurface.cs:163`) is

```csharp
return toCone * new GeoPoint(uv.y * Math.Cos(uv.x), uv.y * Math.Sin(uv.x), uv.y);
```

— the radius grows linearly with `v`, so a cone develops into an annular
sector, a different closed form on the same footing.

## Proposed API

A capability interface, implemented by the surfaces that can support it, so
that callers can test for it the way they already test for `ICylinder` and
`ISurfaceWithRadius` (`CylindricalSurface.cs:11`, `:20`):

```csharp
public interface IDevelopableSurface
{
    /// <summary>
    /// True if this surface can be developed into the plane without distortion.
    /// </summary>
    bool IsDevelopable { get; }

    /// <summary>
    /// Maps a (u,v) parameter pair to its position on the flat pattern.
    /// </summary>
    GeoPoint2D Develop(GeoPoint2D uv);

    /// <summary>
    /// Inverse of <see cref="Develop"/>: flat pattern position to (u,v).
    /// </summary>
    GeoPoint2D DevelopInverse(GeoPoint2D flat);
}
```

with a curve-level and a face-level convenience layer:

```csharp
public static class SurfaceDevelopment
{
    /// <summary>
    /// Develops a curve lying on the surface into the flat pattern,
    /// approximating to the given precision.
    /// </summary>
    public static ICurve2D DevelopCurve(IDevelopableSurface surface,
                                        ICurve curve, double precision);

    /// <summary>
    /// Develops a face into its flat pattern: outline plus inner contours.
    /// <paramref name="seam"/> is the u value at which a fully wrapped face
    /// is cut open; ignored for faces that do not close.
    /// </summary>
    public static CompoundShape DevelopFace(Face face, double precision,
                                            double seam = 0.0);
}
```

Implementations: `CylindricalSurface` and `ConicalSurface` first, `PlaneSurface`
trivially (identity, which makes callers uniform). `SurfaceOfLinearExtrusion`
is developable when its extrusion direction is constant and is a natural
follow-up. Doubly curved surfaces — spheres, tori, general NURBS — return
`IsDevelopable == false`; they cannot be developed without distortion, and
approximating them is deliberately out of scope.

## Details worth getting right

**Elliptical cylinders.** `RadiusX` and `RadiusY` are independent, so
`CylindricalSurface` also represents elliptical cylinders. For those the
circumferential arc length is not `u · r` — it is an incomplete elliptic
integral of the second kind, with no closed form. `Develop` should either
integrate numerically or report `IsDevelopable == false` for the elliptical
case in a first version. The circular case (`RadiusX ≈ RadiusY` within
precision) must not pay for that generality.

**Axial scale.** `v` is the coordinate of the *unit* cylinder, so the flat
coordinate is `v · |Axis|`, not `v`. A `toCylinder` carrying a non-unit axial
scale is easy to overlook and yields a pattern that is wrong by a constant
factor.

**Seam placement.** A face that wraps the full circumference has no boundary in
`u` and must be cut somewhere. The `seam` parameter lets the caller choose,
which matters in practice: a seam placed through a hole splits that hole across
both ends of the pattern. A sensible default is the largest gap in the
projected contour's `u` range, falling back to `u = 0`.

**Periodic curves.** A curve crossing the seam comes back from
`GetProjectedCurve` with a `u` jump of `UPeriod`. It has to be unwrapped before
development, or the flat curve will contain a spurious full-width segment.

**Precision.** `DevelopCurve` approximates; the flat image of a 3D line on a
cylinder is generally not a line. The precision argument should mean the same
thing it does in `GetProjectedCurve`, so callers can reason about one tolerance.

## Testing

- Round-trip: `DevelopInverse(Develop(uv)) ≈ uv` across the natural bounds of
  each supported surface.
- Length preservation: the developed length of a curve matches its 3D length
  within precision — the defining property of a developable surface, and the
  test that catches the axial-scale and radius mistakes above.
- A full cylinder develops to a rectangle of width `2πr` and height equal to
  the face's axial extent.
- A cone develops to an annular sector whose arc length matches the base
  circumference.
- A circular hole on a cylinder develops to a closed, non-self-intersecting
  curve, and one straddling the seam develops correctly for several seam
  choices.
- Elliptical cylinders behave as decided above — either integrated correctly
  against a reference or refused cleanly.

## Scope

In scope: cylindrical, conical and planar surfaces; curve and face
development; the seam and periodicity handling described above.

Out of scope: doubly curved surfaces and any approximate development of them;
bend allowance and K-factor compensation, which are fabrication parameters
rather than geometry; nesting or arranging the resulting patterns.

## Context

This proposal comes out of planning tube-cutting support in an application
built on CADability, but the API is deliberately free of anything specific to
that use, and the capability is one a general-purpose CAD library is expected
to have. Feedback on the shape of the interface — particularly whether a
capability interface or extension methods on `ISurface` fits the library's
conventions better — is welcome before implementation starts.
