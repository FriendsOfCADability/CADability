# Boundary Representation Classes

The "Boundary Representation" (BRep) classes describe solid objects in CADability. They follow the definition outlined in the Wikipedia article [Boundary representation](https://en.wikipedia.org/wiki/Boundary_representation).

The main classes are [Solid](../api/CADability.Solid.html), [Shell](../api/CADability.Shell.html), [Face](../api/CADability.Face.html), [Edge](../api/CADability.Edge.html), and [Vertex](../api/CADability.Vertex.html).

Consider a simple cube: it has six (planar) faces, twelve edges, and eight vertices. The shell consists of the six faces, and the solid is defined by this shell.

## Face

The `Face` class represents a fundamental geometric entity. The most important properties include:

- **Edges** (`IEnumerable<Edge>`): All edges belonging to a face, including those forming the outer boundary and those defining holes.
  - **OutlineEdges** (`Edge[]`): The edges forming the outer boundary of the face, ordered counterclockwise when viewed from the outside.
  - **HoleEdges(int n)** (`Edge[]`): The edges defining the `n`-th hole in the face. A face may contain zero, one, or more holes.
  - **HoleCount** (`int`): The number of holes within the face.
- **Surface** (`ISurface`): The geometric surface of the face. Various classes implement `ISurface`, such as `PlaneSurface`, `CylindricalSurface`, `SurfaceOfLinearExtrusion`, `OffsetSurface`, and `NurbsSurface`, each with properties defining their specific geometric form.

Surfaces are oriented. At every point on the surface, there exists a well-defined normal vector pointing outward. When two faces share an edge, they must maintain the same orientation: traversing from the exterior of one face across an edge leads to the exterior of the adjacent face. This concept of "outside" is essential in defining solids.

## Edge

The `Edge` class represents a boundary segment of a face, either as part of its outer boundary or a hole. Typically, edges connect two faces. Important properties include:

- **Curve3D** (`ICurve`): The geometric curve defining the edge. Various implementations of `ICurve` exist, such as `Line` (a line segment), `Ellipse` (an elliptical arc), and `BSpline` (a NURBS curve).
- **PrimaryFace** (`Face`): One of the faces bounded by this edge.
- **SecondaryFace** (`Face`): The second face bounded by this edge, if present. If only one face is bounded by the edge, `SecondaryFace` is `null`.

Edges have an inherent orientation:
- **Outline edges** follow a counterclockwise order (when viewed from the outside).
- **Hole edges** follow a clockwise order.
- Traversing an edge in its orientation always keeps the associated face to the left.
- Since an edge may connect two faces, its orientation in one face is reversed in the adjacent face. The `Curve3D` orientation is arbitrary, so:
  - **Forward(Face onThisFace)** (`bool`): `true` if the `Curve3D` orientation aligns with the face; otherwise, the reversed curve should be used.
  - **StartVertex(Face onThisFace)** (`Vertex`): The starting vertex of the edge for the given face.
  - **EndVertex(Face onThisFace)** (`Vertex`): The ending vertex of the edge for the given face.

## Vertex

The `Vertex` class represents a connection point between edges. Its key properties are:

- **Position** (`GeoPoint`): The 3D coordinates of the vertex.
- **Edges** (`Edge[]`): The edges for which this vertex serves as a start or endpoint.

## References

Edges, faces, and vertices maintain references to one another. This enables easy navigation:
- From a `Vertex`, all connected edges can be found.
- From an edge, the adjacent faces can be identified.
- From a single `Face`, all connected faces can be determined by traversing shared edges.

## Shell

The `Shell` class represents a collection of connected faces. A shell can be:
- **Open**, if any edge lacks a `SecondaryFace`.
- **Closed**, if every edge has two associated faces.

All faces within a shell must be consistently oriented with their neighbors. A Möbius strip, for example, is **not** a valid shell because its orientation is inconsistent. For each face in a shell all connected faces must also be withim this shell.

## Solid

The `Solid` class represents a solid body, which consists of one or more shells:
- The **first shell** defines the outer boundary, with face normals pointing outward.
- Additional shells may represent internal voids, which are **oriented oppositely**, with face normals pointing inward toward the void.

A well-formed solid ensures that all faces and shells follow these orientation rules, preserving the integrity of the boundary representation.
