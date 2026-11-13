# CADability 1.8.0

CADability 1.8.0 is on [nuget.org](https://www.nuget.org/packages/CADability/1.8.0). Minor release, and the first final release since 1.7.1. It contains everything of the three previews (1.8.0-preview.1 to preview.3) and the changes merged after preview.3:
- the kernel no longer needs GDI+ for texts, pictures and page settings, so it runs on Linux, macOS and in the browser (#412);
- many fixes in the boolean operations, the STEP import and the DXF/DWG import;
- ACadSharp 3.8.49 (FriendsOfCADability fork) instead of 3.8.24;
- faster contour search (`CompoundShape.CreateFromList`) and JSON writing for large drawings (#550).

> **No recompilation needed.** 1.8.0 is source and binary compatible with 1.7.1. A comparison of the public API of the 1.7.1 package with this build (ApiCompat) finds nothing removed and no signature changed; public API is only added. A few members are newly `[Obsolete]` (see *Deprecated*). They only cause compiler warnings, which become errors in projects that treat warnings as errors.
>
> **Check the behaviour changes before updating**, especially if your application does not use CADability.Forms: its texts now get their fonts from the managed font reader instead of GDI (see *Behaviour changes*).

## Highlights

- **Texts without GDI** (#466, #468, #470, #471, #472, #500):
  - Texts and dimensions get their fonts from a font service of the kernel, `FontProvider.Current`, instead of GDI.
  - The new managed reader reads TrueType and OpenType fonts (`.ttf`, `.ttc`, `.otf`, `.otc`) on Windows, Linux and macOS, with no new dependency: TrueType and CFF outlines (also CID-keyed), composite glyphs, symbol fonts, kerning, and the named instances of variable fonts.
  - When a family has no bold or italic face, the reader synthesizes it as GDI+ does.
  - Fonts can also be added from files or bytes with `ManagedFontProvider.AddFontFile` and `AddFontData`.
- **Windows Forms applications keep their texts as before** (#503): as soon as one of `CadControl`, `CadCanvas`, `CadFrame`, `PaintToOpenGL` or `PaintToGDI` of CADability.Forms is used, `GdiFontProvider` becomes the font provider, which gets the fonts from GDI and GDI+ as CADability did up to 1.7.1. A provider the application sets itself is always kept.
- **Pictures and page settings without GDI+**: `Picture` and `Icon` keep the bytes of their PNG, JPEG, GIF or BMP file instead of a GDI+ bitmap (#473). Layouts and projects keep their page settings in the new `PageSetup` class instead of the GDI+ `PageSettings`, so projects with page settings can be saved on .NET 8 again (#474).
- **The kernel runs in the browser** (#484): the Linux CI runs `CADability.dll` as WebAssembly under Node.js.
- **Boolean operations**:
  - Cube and sphere unions and differences that failed now work, among them a sphere axis in a face of the cube, an edge crossing the seam of the sphere, and a pole on the intersection (#521, #525 to #528).
  - Oblique frustums from `MakeRuledSolid` (#515, #517), faces of extrusions and ruled surfaces (#513, #516), and mitered bars with round holes meeting on the common cut (#512, #516) give correct results.
  - Booleans of tiny tools on big solids, with size ratios down to 1:10^10, give the exact result instead of throwing a `ModOpException` (#532).
- **Reproducible STEP import** (#522): with `StepImport.Parallel` (on by default) the faces of a shell are made one after the other, and only separate parts are made in parallel. The same file now gives the same objects every time. Before, the order of faces, edges and vertices changed, and booleans on the result could differ or throw now and then.
- **DXF/DWG**:
  - Text heights are converted with the cap height of the font, as DXF and DWG define them, so exported texts are no longer 1.4 to 1.6 times too large in other programs, and a round trip keeps `TextSize` (#506, #507).
  - The import is protected against nested blocks that expand without limit (#488, #533).
  - The embedded ACadSharp moves to 3.8.49 (see *ACadSharp*).
- **Speed** (#550, #521, #523):
  - `CompoundShape.CreateFromList` is no longer quadratic in the number of open curves and holes: 0.17 s instead of 15.4 s for 400 parts with 10 holes each, in the pull request's measurements.
  - `Project.WriteToJson` caches an attribute lookup it did by reflection for every value, about 4 times faster for a large DXF import.
  - NURBS curves and surfaces are evaluated without allocating, and surface derivatives are computed for the knot span of the point only.

## New API

All of it is added; nothing existing changes.

- `FontProvider` (`Current`, `GetFamilyNames`, `GetFace`, `GetDefaultFace`) and `FontFace` (metrics, `GetAdvanceWidth`, `GetKerning`, `GetGlyphPath`), both abstract, so an application can supply its own fonts (#468).
- `ManagedFontProvider` with `AddFontFile`, `AddFontData` and `SystemFontDirectories` (#468).
- `Picture.ImageData`, `PixelWidth`, `PixelHeight`, `Set(byte[], GeoPoint, GeoVector, GeoVector)` and `SetImageDataNoUndo(byte[])` (#473).
- `Icon.ImageData`, `PixelWidth`, `PixelHeight` and `Set(byte[], GeoPoint, int, int)` (#473).
- `PageSetup` (paper name, kind and size in mm, orientation, margins, printer name), `Project.DefaultPageSetup` and `Layout.PageSetup` (#474).
- `Project.WriteToFile(string FileName, bool useProgress)`: saves without showing the progress bar, e.g. from a background thread (#509).
- `SettingsGlobalFileName.neverLoadGlobalSettings` (default `false`): when set before `Settings` is first used, the global settings file is not read and the settings start with their defaults. `neverSaveGlobalSettings` keeps its meaning and only suppresses saving (#538, #541).
- `Border.GetMinDistance(GeoPoint2D)` is public (it was `internal`): the distance from a point to the outline, the same inside and outside (#545).
- In CADability.Forms, which is not part of the package: `GdiFontProvider` (#468, #503).

## Fixed issues

#412 kernel without GDI+ for texts, pictures and page settings · #488 DXF/DWG files whose nested blocks expand without limit · #505 right click on a picture threw · #506 DXF text heights · #509 saving from another thread threw · #511 orientation of the arc side faces of `MakePrism` · #512 mitered bars with equal holes · #513 booleans on extrusion faces · #514 `MakePrismMiter` always threw · #515 general intersection curve ended early · #518 `DecomposeAll` · #538 an application could not keep CADability from reading the global settings file (see `neverLoadGlobalSettings`) · #542 a selected layout patch could no longer be moved on the paper.

## Other fixes

- **`GeoObjectList.DecomposeAll`** threw `ArgumentOutOfRangeException` when the last object decomposed into nothing, e.g. an empty block, skipped objects, dropped new hatches and left nested block references undecomposed. `DecomposeBlocks(bool)` had the same mistake (#518, #519).
- **`Solid.Volume`**:
  - It was noise for a small solid far from the origin (#532).
  - It depended on the precision asked for before when a face triangulation reached its limit of 100000 triangles (#527).
- **`ShellMetrics.IntegratedVolume`** was wrong for sphere faces bounded at a pole (#528).
- **`Geometry.LineFit`** (2d and 3d) fitted the best line through the origin instead of through the points (#521).
- **`NurbsSurface.FixedU` and `FixedV`** threw while a file was being read; **`Shell.RecalcEdges`** no longer fails `GetOffset`, `MakeThick` and shell copies on edge curves whose ends cannot be moved (#521).
- **Project files** (#531, ported from ShapeIt): a `List<Solid>` stored as a value of a `Dictionary<string, object>`, for example in `UserData`, made the whole file unreadable; nested lists came back empty and two-dimensional array properties were not read. All of them are restored now.
- **Cloned shells shared their feature names** with the original, so removing a feature from the clone removed it from the original too (#531).
- **`GeneralCurve2D.Intersect`** found only crossings; points where two 2d curves touch are found now (#531, ported from ShapeIt).
- **`Face.MakeRegularSurface`** simplifies faces on exact rational NURBS cylinders, cones, spheres and tori again; faces with a seam are still left unchanged (#534).
- **STEP import of closed B-spline curves**: moving the start point of a periodic spline gave the right end points but a wrong shape in between. The STEP import uses this for edges that start somewhere along a closed curve, so such edges could be imported with a wrong shape. Shown on constructed splines; no real STEP file with the error is known (#548).
- **Layout views (CADability.Forms)**: selecting a layout patch or one of its entries in the control center lets the patch be dragged on the paper again (#542, #543). With objects selected, the control center no longer jumps to an empty "Action" page, and switching tabs afterwards no longer throws an `IndexOutOfRangeException` in `PropertyPage.GetCurrentSelection` (#547).
- **English UI texts** have been reviewed against the German ones: typos, wrong meanings, German words left in, and duplicate ids are fixed.

## Behaviour changes

- **Fonts outside Windows Forms** (#468, #472, #503):
  - Applications that use the kernel without the classes of CADability.Forms listed above now get their fonts from the managed reader, on Windows too. This applies to renderers implemented directly against `IPaintTo3D`, to front ends of their own, to servers and to the browser.
  - Texts can differ slightly from the GDI rendering, mostly in hinting and in synthetic bold. To keep GDI fonts in such an application, set `FontProvider.Current` at startup, e.g. to `new CADability.Forms.GdiFontProvider()` or to a provider of your own.
  - A text whose font family is not installed falls back to a default face. On a machine without any font, texts are not painted, and `GetOutline`, `GetShapes` and `GetCenterline` of a text throw `InvalidOperationException`.
  - The font lists in the property editors and dimension styles show the families of the current provider.
- **Pictures** (#473):
  - Project files store a picture as the bytes of its image file instead of a serialized GDI+ bitmap. Files of earlier 1.x versions open as before, also on Linux and macOS.
  - Without GDI+, a picture is painted as its outline and an icon is not painted.
- **Page settings** (#474): new files contain only the neutral `PageSetup`; earlier CADability versions ignore the page settings of such files and read everything else. The page and printer dialogs of a layout view always store their result in the layout.
- **STL export**: texts are left out of STL files, on every platform (#479).
- **Imported DXF and DWG texts and dimensions are drawn larger** than in 1.7.1: as large as AutoCAD shows them, because the height in the file is now read as the cap height (#506).
- **New setting `DxfDwg.MaxImportObjects`** (default 1,000,000): DXF and DWG files whose blocks expand to more objects are no longer imported. The import stops with an `ApplicationException` whose message names the setting, and the handlers of single entities no longer swallow it or an `OutOfMemoryException`. Raise the setting if you import such drawings on purpose (#488, #533).
- **DXF/DWG import of an `INSERT` with a scale factor of 0**: it is skipped, as before; newer ACadSharp versions would otherwise build a singular transformation (#520).
- **STEP import** (#522): `StepImport.Parallel` no longer makes the faces of one shell in parallel. A single solid with 3000 faces takes about 10 % longer; assemblies are as fast as before.
- **Default styles get their attributes**: `StyleList.Default` named its colour, line width and line pattern in lower case, and the case-sensitive lookup never found them. The default styles now have these attributes, in English and German.
- **Geometry** (#521, #525, #531, #534):
  - `GeneralCurve.DistanceTo` returns the distance to the closest end for a point beyond an end of the curve, instead of the distance to the extension of the curve.
  - A BSpline whose poles lie within `Precision.eps` of a plane, but not exactly in it, is evaluated in 3d.
  - `GeneralCurve.GetProjectedCurve` keeps a copy of the curve.
  - `Shell.CombineConnectedFaces` no longer combines faces with a pole edge.
  - 2d curve intersections can return additional points where curves touch.
  - `Face.MakeRegularSurface` returns `true` and changes the surface for faces on rational NURBS, where it returned `false`.
- **Mixed dictionaries in project files** (#531): `Dictionary<string, object>` and `Hashtable` values of mixed types are written with the type of each value (`$ValueTypes`).
- **`IPropertyPage.GetParent` and `IPropertyEntry.Parent` in CADability.Forms** (#543): the `PropertyPage` of CADability.Forms now sets the parent of a sub entry to the entry it is shown under, as documented; before, it was always the page, and `GetParent` returned `null` for every sub entry. `LayoutPatch` now follows `IPropertyPage.OnSelectionChanged` and walks the `Parent` chain to set its `ModifyLayoutPatch` action. Applications with an `IPropertyPage` of their own get this behaviour only if their page sets `Parent` and raises `OnSelectionChanged` in the same way.

## Deprecated

Marked `[Obsolete]`. These members still work on Windows, and no removal is scheduled:
- `Picture.Bitmap`, `Picture.Set(Bitmap, …)`, `Picture.SetBitmapNoUndo` and `Icon.Set(Bitmap, …)`: use the `byte[]` members (#473).
- The field `Layout.pageSettings`: use `Layout.PageSetup` (#474).

## ACadSharp

The embedded ACadSharp moves from 3.8.24 (1.7.1) to **3.8.49**, built from the FriendsOfCADability fork, which keeps the R10–R12 DWG reader and the TEXT group 11 fix of 1.7.1 on top (#510, #520, #549).

- **Why 3.8.49 and not the newest upstream version** (#549): upstream reworked its DXF reader right after 3.8.49. From that rework on, R12 DXF files read wrongly: the centres of ARC entities become (0,0,0), and the definition points of linear DIMENSION entities are lost. The fork stops at 3.8.49 until this is fixed upstream.
- **Upstream changes 3.8.25 to 3.8.37** (#510, #520): OLE2FRAME entities with missing data no longer stop the reading of a drawing; 3DSOLID/ACIS data is read and written; gradient hatches are written to DXF.
- **Upstream changes 3.8.38 to 3.8.49** that can reach CADability's import and export (#549):
  - DWG: trailing empty pages of R2004+ sections and the MEASUREMENT value of R13–R15 files are read correctly.
  - The DXF writer writes the fit data of hatch spline edges only from AC1024; older DXF versions received invalid data there.
  - Proxy graphics: true colour no longer has red and blue swapped, shell holes are kept, and polyline vertices are no longer shifted.
  - An ellipse radius ratio a rounding error above 1 is accepted.
  - Tables clone deeply and keep their styles, and the visual style of a viewport is resolved.
- **What it means for the import**: in the tests of #549, all 27 R10–R12 sample drawings and a production R12 drawing gave exactly the same output as with 3.8.37, and the 14 DXF sample files of ACadSharp gave the same geometry. The DXF/DWG versions CADability accepts are unchanged.

ACadSharp stays embedded in the package and is not a package dependency.

## Package

`CADability.1.8.0.nupkg` contains `CADability.dll` and `CADability.xml` for netstandard2.0, with `ACadSharp.dll` and `ACadSharp.xml` 3.8.49 (FriendsOfCADability fork) embedded. Dependencies are unchanged from 1.7.1: MathNet.Numerics.Signed 5.0.0, System.Drawing.Common 6.0.0, System.Memory 4.6.3 and System.Text.Encoding.CodePages 9.0.6. Symbols are on the nuget.org symbol server (`CADability.1.8.0.snupkg`, Source Link). CADability.Forms is not part of the package.

## Known limitations

- **Synthetic bold differs slightly from GDI+** in a few fonts (Sylfaen, Gabriola, some named instances of variable fonts). This only matters where the managed reader is used (#500).
- **Font formats not read** by the managed reader: colour emoji fonts and the deprecated `seac` accent composition of old CFF fonts (#470). Variable fonts are available through their named instances only (#471).
- **Images without GDI+**: the kernel does not decode images, so renderers other than GDI+ show pictures as their outline (#473, #484).
- A face triangulation still stops at 100000 triangles; `Solid.Volume` then falls back to a coarser triangulation of all faces (#527).
- The triangulation of ruled faces between splines, and of NURBS faces with the same geometry, can be several percent too large when the surface is strongly twisted. Booleans are not affected, since they use the exact surfaces.
- ACadSharp's DWG reader can still be made to loop or allocate gigabytes by corrupt R2004+ files, its DXF reader by a corrupt MESH, and its MText text processor by a format code without `;` (#489, #490, #491). The ACadSharp DXF writer writes XData layer references as handles (#460).
- Open issues: #489, #490, #491, #460, #408, #409, and the remaining limitations of the boolean sweep listed in `tests/CADability.BooleanSweep/README.md`.
