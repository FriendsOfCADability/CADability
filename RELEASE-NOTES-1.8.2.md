# CADability 1.8.2

CADability 1.8.2 is on [nuget.org](https://www.nuget.org/packages/CADability/1.8.2). Patch release with a fix for a crash in `Border.ConvexHull`, working focus handlers in the property pages, and layouts that can be shown outside Windows.

> **No recompilation needed.** 1.8.2 is source and binary compatible with 1.8.1: no public API is added, removed or changed. `DrawingPlaneProperty` now overrides `Added` and `Removed`, which it inherited before; callers are not affected.

## Fixed issues

#544 the focus handlers of several property entries never ran · #561 (step 1) showing a layout threw outside Windows.

## Fixes

- **`Border.ConvexHull`** (#563): no longer throws a `NullReferenceException` when a segment reports an inflection point at or very close to its start, e.g. through numerical noise on very flat arcs or splines. `Arc2D.Trim` and `BSpline2D.Trim` return `null` for such an empty part, and that `null` ended up in the segment list. `ConvexHull` now splits only at inflection points strictly inside a segment and skips empty parts. `GetSmallestEnclosingRectangle`, which calls `ConvexHull`, crashed the same way.
- **Property pages** (#546): the control center never raises `IPropertyTreeView.FocusChangedEvent`, so the focus handlers of `Grid`, `DrawingPlaneProperty`, `GeoObjectListProperty`, `Schedule` and `ModelView` never ran. They are replaced:
  - `Grid` raises `GridChangedEvent` when the edit of a distance ends (Enter, Tab, Escape or selecting another entry), and only if a distance actually changed.
  - `DrawingPlaneProperty` applies a typed x or y direction when the selection leaves that entry or the entry is removed from the page, then repaints. Its entries no longer show a stale direction after the plane was set by a menu command or an action.
  - `ModelView` applies a modified projection direction or distance when the selection leaves the projection direction or the view entry is removed from the page. A (0, 0, 0) direction is rejected.
  - Selecting a `GeoObjectListProperty` or one of its objects in the control center selects them in the `AnimatedView` again.
- **Grid distance 0** (#546): `ModelView.ShowGrid` no longer loops forever when a grid distance is 0, e.g. while "0.5" is being typed; the grid is not drawn then.
- **Layouts outside Windows** (#562): the `LayoutView` constructor created a GDI+ `PrintDocument` right away, so showing a layout threw a `PlatformNotSupportedException` (wrapped in a `TypeInitializationException` of `System.Drawing.Printing`) outside Windows. The print document is now created by the print, page setup and printer commands when they first need it, with the same settings as before: those of the layout, else those of the project, else the orientation of the layout's paper.

## Behaviour changes

- `Grid.GridChangedEvent` is raised once when the edit of a distance ends, not for every keystroke and not when a distance is left unchanged (#546).
- The print, page setup and printer commands of a layout view use the view's own print document. They used to fall back to the project's print document only in a case that could not happen, because the constructor had always created one (#562).

## Other changes

- A security policy (`SECURITY.md`) is added: vulnerabilities are reported privately through GitHub's private vulnerability reporting, and security fixes go into the latest 1.x release (#560).

## Package

`CADability.1.8.2.nupkg` contains `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded, as in 1.8.1. Dependencies are unchanged from 1.8.1. Symbols are on the nuget.org symbol server (`CADability.1.8.2.snupkg`, Source Link).
