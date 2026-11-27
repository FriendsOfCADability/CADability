# CADability 1.8.5

CADability 1.8.5 is on [nuget.org](https://www.nuget.org/packages/CADability/1.8.5). This patch release makes rotating the view with the mouse smooth again (#568).

> **1.8.4 was not published.** Its version number and release notes were prepared, but the package never reached nuget.org. 1.8.5 is the first package after 1.8.3 and therefore also contains all fixes of [1.8.4](RELEASE-NOTES-1.8.4.md): surface, curve and JSON fixes from the ShapeIt fork. Read those notes as well when you update from 1.8.3, in particular their behaviour changes.

> **No recompilation needed.** 1.8.5 is source and binary compatible with 1.8.3: no public API is added, removed or changed.

## Fixes

- **Rotating the view with Ctrl and the middle mouse button was slow** (#568, #569), noticeably slower than in the versions before CADability went open source. Every mouse move of the rotation called `ModelView.ForceInvalidateAll`, and with it `ProjectedModel.RecalcAll`. That method set the cursor of the canvas to the wait cursor, and right after it the view set the pan cursor again. On Windows, with the cursor of the control changing twice per mouse move, the view was painted only rarely during the rotation. The method also visited every object of the model, so each mouse move took longer the more objects the model has: about 8.6 ms with 9,800 objects. The old versions set the cursor only temporarily (`Cursor.Current`) and painted synchronously after each step; both got lost when the view was moved behind `ICanvas`.

  A new view direction needs none of this work, because the display lists are in world coordinates. While the mouse rotates the view, the view is now only repainted, which takes a constant 0.035 ms per mouse move. The scroll position is updated once, when the rotation ends. This applies to parallel and perspective views, to `ModelView.SetViewPosition` and to `ViewPointAction`.
- **The center of the rotation** (used when no `ModelView.FixPoint` is set; #569): the view picks the object in the middle of the view and rotates around it. Since 2025 this pick ran on every mouse move and used the y coordinate of the center of the view as its x coordinate. In a view that is wider than high, it therefore picked left of the center and usually missed, and the view rotated around the drawing plane instead of the object in the middle. The center is now picked once, when the rotation starts, at the right position. Objects on hidden layers are ignored.
- **`ProjectedModel` was connected to the events of its model twice** (#570), once by the constructor and once more by `Connect`. Every change of an object raised the repaint request twice, and every event of the model (object added or removed, extent changed, new display lists) was handled twice. It is now connected once.

## Behaviour changes

- `ProjectedModel.SetViewDirection(…, bool temporary)`: the `temporary` parameter no longer has any effect. The display lists were never rebuilt for a new view direction; `false` only connected the change events of the objects again.
- `ProjectedModel.ForceRecalc` and `ModelView.ForceInvalidateAll` mark the display lists of the projected model as invalid and discard its extent. They no longer visit all objects of the model, and they no longer set the wait cursor.
- `ModelView.Recalc` only updates the scroll position.
- `ModelView.SetViewDirection(ModOp, GeoPoint, bool mouseIsDown)` with `mouseIsDown == true` only repaints the view. A caller that rotates the view itself must update the scroll position when the rotation ends. The view does this with the next mouse move that does not rotate, as it does after its own rotation.

## Package

`CADability.1.8.5.nupkg` contains `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded, as in 1.8.3. Dependencies are unchanged from 1.8.3. Symbols are on the nuget.org symbol server (`CADability.1.8.5.snupkg`, Source Link).
