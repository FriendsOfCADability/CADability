# CADability Graphics Architecture — Modernization Proposal

**Status:** Draft for discussion (no code yet) · **Scope:** replaces the legacy OpenGL 1.1 rendering stack · **Related:** PR #339 (Silk.NET port), PR #340 (triangulation memory fixes), issues #256 (CADability 2.0 ideas), #308 (display-list churn), #62, #37

---

## 1. Executive summary

CADability's interactive renderer is an OpenGL **1.1 fixed-function** pipeline: immediate-mode `glBegin/glEnd` calls recorded into `glNewList` display lists, driven through the ~60-member `IPaintTo3D` painting interface, on top of a 51,940-line hand-copied P/Invoke binding (`CADability/OpenGL.cs`) that is compiled into the platform-neutral core. Text goes through `wglUseFontOutlines`, contexts through WGL with process-static sharing, and any change to any object invalidates the display lists of the entire model.

Moving the same architecture onto modern bindings (PR #339, Silk.NET + GLSL shaders) improves the plumbing but keeps the underlying problems: a chatty per-primitive painting contract, record/replay display-list semantics, model-global invalidation, no culling, and re-tessellation storms on zoom.

This document proposes a ground-up redesign of *how graphics are handled*, while migrating incrementally:

1. A **retained scene layer** replaces display lists: tessellation workers produce indexed triangle/polyline meshes (the data `Face.GetTriangulation` already emits today) into pooled GPU buffers; objects become lightweight `RenderItem`s with material/transform table entries; invalidation is **per object**, culling is per frame, LOD is discrete and memory-bounded.
2. A **thin, WebGPU-shaped rendering hardware interface (RHI)** owned by CADability abstracts the GPU APIs. Commands are recorded into a compact binary command buffer — which is also what makes a **browser (WebAssembly + WebGL2/WebGPU) backend** practical, since the whole frame crosses the JS boundary in a handful of calls.
3. **Backends** plug in beneath the RHI: desktop OpenGL 3.3 via Silk.NET first, browser WebGL2 second, WebGPU (desktop `wgpu-native` + browser) later. **On Windows the default runtime is DirectX from day one**: the GL backend runs over ANGLE (GL-translated-to-D3D11 — the same battle-tested path every browser uses), with a native D3D11 backend and WebGPU-on-D3D12 as further routes (§4.5). The RHI's conventions are WebGPU-shaped and therefore closer to D3D than to GL — DirectX is a first-class fit, not an afterthought.
4. An **`IPaintTo3D` compatibility adapter** keeps every existing view, sink and downstream application working during the migration. The legacy GL code is deleted only in the final phase.

Constraints this design was written against (agreed with the maintainers up front):

| Constraint | Decision |
|---|---|
| Platforms | **Web is a first-class target now** (WASM + WebGL2/WebGPU), alongside Windows/Linux desktop |
| GPU abstraction | **Own thin RHI**, WebGPU-shaped; no third-party renderer lock-in |
| Migration | **Incremental**; repo shippable at every phase; legacy deleted at the end |
| Scale | **Millions of triangles interactive** (batching + frustum culling + coarse LOD; not 10M+ streaming/occlusion) |

---

## 2. Current state — what exactly is wrong

All references verified on `master` (July 2026).

### 2.1 The painting contract

`IPaintTo3D` (`CADability/PaintToOpenGl.cs:58-397`) is an immediate-mode interface: state setters (`SetColor`, `SetLineWidth`, `SetLinePattern`, `PushMultModOp`, `Blending`, …) followed by per-primitive calls (`Polyline`, `Triangle`, `Arc`, `Text`, …), optionally recorded between `OpenList`/`CloseList` into an opaque `IPaintTo3DList`. Its own doc-comment (line 54) has anticipated replacement for two decades: *"Interface to paint on a OpenGL, DirectX, GDI or some other output device. This interface may still change in future."*

Its ~60 members group as follows:

| Group | Members (abridged) |
|---|---|
| Capability / mode properties | `PaintSurfaces`, `PaintEdges`, `PaintSurfaceEdges`, `UseLineWidth`, `Precision`, `PixelToWorld`, `SelectMode`, `SelectColor`, `TriangulateText`, `Capabilities`, `IsBitmap` |
| Render state | `MakeCurrent`, `SetColor`, `AvoidColor`, `SetLineWidth`, `SetLinePattern`, `SetProjection`, `Clear`, `Resize`, `UseZBuffer`, `Blending`, `SetClip`, `PushState`/`PopState`, `PushMultModOp`/`PopModOp`, `PaintFaces`, `FacesBehindEdgesOffset` |
| Primitives | `Polyline`, `Points`, `Triangle(vertices, normals, indices)`, `Line2D`, `FillRect2D`, `Point2D`, `Arc`; GDI-only path ops `OpenPath`/`ClosePath`/`CloseFigure` |
| Display lists | `OpenList`, `CloseList`, `MakeList`, `List`, `SelectedList(list, wobbleRadius)`, `FreeUnusedLists`, `FinishPaint` |
| Text & images | `PrepareText`, `Text`, `PreparePointSymbol`, `PrepareIcon`, `PrepareBitmap`, `RectangularBitmap`, `DisplayIcon`, `DisplayBitmap` |
| Deprecated | `DelayText`, `DelayAll`, `DontRecalcTriangulation`, `Nurbs`, `FilledPolyline` |

It is used in **two distinct roles**, and any modernization must respect both:

- **Callers** — every `IGeoObject` implements `PaintTo3D(IPaintTo3D)` and emits its geometry through these members. This includes third-party custom GeoObjects in downstream applications.
- **Implementers** — rendering *sinks* implement the interface and receive the model's geometry: in-tree `PaintToOpenGL`, `PaintToGDI`/`PaintToBitmap`, `PrintToGDI`, `PaintToSTL` — and, importantly, **third-party rendering engines built by downstream users** (at least one such engine is known to exist in production). For them, `IPaintTo3D` is not an internal detail but the product's rendering SPI. Section 7.4 defines the compatibility contract this proposal commits to.

Problems baked into the contract itself:

- It transports geometry **call-by-call** (per polyline, per triangle batch) instead of as bulk buffers — expensive on desktop, prohibitive across a JS-interop boundary.
- It leaks backend concepts: `Blending(bool)` is documented "OpenGL specific"; `FacesBehindEdgesOffset`/`PaintFaces` encode the edges-over-faces matrix-translation trick as interface contract; `SelectedList(list, wobbleRadius)` encodes one specific highlight implementation.
- It is typed against `System.Drawing` (`Color`, `Bitmap`, `PointF`, `Rectangle`, `FontStyle`) — ~98 files in the core `using System.Drawing`, with a bit-rotting `#if WEBASSEMBLY` fork (69 files) that swaps in `CADability.WebDrawing`.
- Capability flags (`PaintCapabilities.CanDoArcs`, `CanFillPaths`, `ZoomIndependentDisplayList`) force behavioral branches at call sites because the five implementations genuinely behave differently.

### 2.2 The OpenGL backend

`CADability.Forms/PaintToOpenGL.cs` (2,278 lines) is strictly fixed-function GL 1.1:

- Immediate mode everywhere (`glBegin(GL_TRIANGLES)` with per-vertex `glNormal3d`/`glVertex3d`), compiled into display lists. No VBOs, no VAOs, no shaders, no `glDrawElements` — zero occurrences.
- Windows-only context handling: WGL + `PIXELFORMATDESCRIPTOR`, process-static context bookkeeping (`MainRenderContext`, `activeRenderContexts`), display-list sharing via `wglShareLists` (with a comment admitting *"geht nur sorum, keine Ahnung warum"*). Context loss in dock/tab scenarios is a known production failure mode (issue #37, #62).
- Text: each glyph compiled into its own display list via `wglUseFontOutlines`, one `glCallList` per character.
- Resource lifetime fights the GC: display lists can only be deleted on the GL thread, so finalizers enqueue IDs into static `toDelete` queues drained later; `GL_OUT_OF_MEMORY` is caught and painting continues with incomplete lists.
- The 51,940-line `CADability/OpenGL.cs` (a copied Tao Framework binding) plus `Gdi.cs` live **in the core assembly**, so even the "portable" kernel carries the whole Win32 interop surface.

### 2.3 The display-list pipeline

- `Model` owns one GL display list per **layer × {face, transparent, curve}** plus zoom-independent objects (`Model.cs:118-124`). That is the only cross-object batching in the system.
- Invalidation is **model-global and unconditional**: `Model.OnGeoObjectDidChange` sets `displayListsDirty = true` for *any* change, including attribute-only changes (`Model.cs:1656-1661`). Recoloring one object re-runs `PaintTo3D` over **every** object in the model into freshly compiled lists. (The neighbouring octree code *does* distinguish `OnlyAttributeChanged` — the display lists never got that granularity; the code comment at `Model.cs:1637` admits it.)
- LOD is zoom-driven re-tessellation: `ModelView` derives `Precision` from the zoom factor (`ModelView.cs:799-804`); when it drops below the cached precision a background thread re-triangulates *everything* at `precision/2`, and any model change aborts and restarts it. PR #340 documents the result: multi-GB allocation churn from abort cascades and speculative rounds, plus per-paint re-computation of all vertex normals (`Face.cs:6913-6919`).
- **No render-side culling.** The octree (`OctTree.cs`) is used for picking/snapping only; painting replays entire per-layer lists every frame regardless of the viewport.
- Selection/hover: selected objects get an extra display list replayed with a stencil-buffer "wobble" halo; hover feedback **rebuilds a display list every frame**, one per object per view (`SelectObjectsAction.cs:535-542`) — issue #308 measured the churn ("Delete List" storms, slow rotation).

### 2.4 What is already right (and reusable)

- **Tessellation output is pipeline-ready.** `Face` caches indexed triangles + UVs (`trianglePoint`/`triangleUVPoint`/`triangleIndex`, `Face.cs:5766-5770`); `GetTriangulation` (`Face.cs:8624`) hands out exactly what a vertex/index buffer needs — `ExportToThreeJs.cs:337` already consumes it as WebGL-ready arrays. Only the *transport* (immediate mode) is legacy.
- **Clean seams exist.** `ICanvas` (`ModelView.cs:41`) abstracts the control; backend choice is a single switch on `IView.PaintType` in `CadCanvas.ShowView` (`CadCanvas.cs:121-151`); `IUIService.CreatePaintInterface` abstracts offscreen rendering; `CADability.Substitutes` (`FormsSubst.cs`) already decouples core input events from WinForms; `WebDrawing.cs` is a from-scratch replacement for the System.Drawing value types.
- **Retained rendering is already proven in-tree.** `AnimatedView` bakes display lists once per `IDrive` and animates purely with matrix pushes (`AnimatedView.cs:49, 325-342, 794-831`) — smooth precisely because it *avoids* the rebuild path. The new architecture generalizes this to everything.
- Picking is CPU-geometric (octree + `Projection.PickArea`) and fully portable — it survives unchanged.

### 2.5 Why the Silk.NET port is not enough

PR #339 replaces the binding layer (`PaintToSilkGL`, GLSL 330, VBO-backed display-list emulation) but keeps `IPaintTo3D` semantics: per-primitive calls, record/replay lists, model-global invalidation, per-frame feedback rebuilds, Windows-only context glue (`WglContext`). It is a useful proof that shaders can render CADability's content — and its context/shader learnings feed directly into Phase 1 below — but it cannot deliver "smooth and fast," and it cannot reach the browser. The two efforts are complementary: **PR #340 is a prerequisite** (the scene layer depends on cached normals and tamed triangulation), **PR #339 is superseded** by this proposal's Phase 1.

---

## 3. Target architecture

### 3.1 Layering

```
+------------------------------------------------------------------+
| Hosts: CADability.Forms (WinForms) | Browser host | Avalonia (future)
|        native window/canvas + input  ->  CADability.Substitutes
+------------------------------------------------------------------+
| Views (core, API unchanged): ModelView / AnimatedView / LayoutView
|        overlay painters: grid, rubber band, action feedback
+------------------------------------------------------------------+
| CADability.Rendering                                             |
|   ViewScene   per view: camera, visible layers, selection/hover  |
|               id sets, frustum culling, LOD choice, draw lists   |
|   SceneGraph  per Model: RenderItems, material & transform tables|
|   TessellationCache  (object, lodLevel) -> CpuMesh, worker pool  |
|   FrameComposer      pass sequence + overlay stages              |
|   PaintTo3DToScene   IPaintTo3D compatibility adapter            |
+------------------------------------------------------------------+
| RHI: IRhiDevice, buffers, textures, pipelines, bind groups,      |
|      command encoder, surfaces/framebuffers (WebGPU-shaped)      |
+------------------------------------------------------------------+
| Backends: .OpenGL (Silk.NET GL 3.3; Windows default via         |
|                    ANGLE -> Direct3D 11)                         |
|           .WebGL  (JSImport + JS executor)  browser              |
|           .D3D11 (optional native) | .WebGPU (D3D12)  later      |
+------------------------------------------------------------------+
| Geometry kernel (CADability): Face/Edge/Curve/Model/Projection   |
|   produces triangles & polylines; owns the picking octree        |
+------------------------------------------------------------------+
```

Data flows one way: kernel geometry → tessellation cache → scene graph → per-view draw lists → RHI command stream → backend. Change events flow the other way and invalidate **per object**, never per model.

### 3.2 Projects and target frameworks

| Project | Contents | TFM |
|---|---|---|
| `CADability` (existing) | Kernel. Eventually loses `OpenGL.cs`, `Gdi.cs`, `System.Drawing.Common`. `IPaintTo3D` stays (public API) through the migration. | `netstandard2.0;net8.0` (multi-target; see open question 1) |
| `CADability.Rendering` (new) | RHI types, SceneGraph, RenderItem, TessellationCache, ViewScene, FrameComposer, MeshBuilder, `PaintTo3DToScene`, embedded shader sources. **No P/Invoke, no System.Drawing, no windowing.** | `netstandard2.0;net8.0` |
| `CADability.Rendering.OpenGL` (new) | Desktop backend: Silk.NET.OpenGL (GL 3.3 core / GLES3) + ~200 lines of EGL/WGL/GLX surface glue owned by us. **On Windows it runs over ANGLE by default, so rendering executes on Direct3D 11** (§4.4); raw vendor GL stays selectable, Linux uses GLX/EGL. | `net8.0` |
| `CADability.Rendering.WebGL` (new) | Browser backend: `[JSImport]` interop + `webgl-executor.js` command interpreter (~500 lines JS). | `net8.0-browser` |
| `CADability.Rendering.WebGPU` (later) | wgpu-native (desktop, **D3D12 on Windows**) and browser WebGPU via the same executor scheme. | `net8.0` / `net8.0-browser` |
| `CADability.Rendering.D3D11` (optional, later) | Native Direct3D 11 backend via Silk.NET.Direct3D11 + DXGI flip-model swapchains; HLSL ports of the shader set (§4.4 route 2). | `net8.0-windows` |
| `CADability.Forms` (existing) | WinForms host; `CadCanvas` gains the new backend branch; keeps `PaintToGDI`; receives `PrintToGDI` when the core drops System.Drawing. | `net8.0-windows` |
| `CADability.Host.Browser` (new, sample) | Minimal browser app: canvas, DOM events → Substitutes, `requestAnimationFrame` loop. | `net8.0-browser` |
| `CADability.Host.Avalonia` (future) | Avalonia control hosting a render surface. | `net8.0` |

Windowing/input stay host-owned: hosts translate native events into the existing `CADability.Substitutes` types and hand the renderer an `IRhiSurface` created from a native handle or canvas id — the renderer never sees an HWND, `Control`, or DOM object.

---

## 4. The RHI

### 4.1 Interface surface (sketch)

WebGPU-shaped, deliberately minimal — only what a CAD renderer needs. Everything lives in `CADability.Rendering.Rhi`:

```csharp
public interface IRhiDevice : IDisposable
{
    RhiCapabilities Capabilities { get; }                 // limits, multiDraw, instancing, float targets…
    IRhiBuffer        CreateBuffer(in BufferDescriptor d);        // Vertex|Index|Uniform, Static|Dynamic
    IRhiTexture       CreateTexture(in TextureDescriptor d);      // 2D, RGBA8 / Depth24Stencil8 / R32Uint
    IRhiSampler       CreateSampler(in SamplerDescriptor d);
    IRhiShaderModule  CreateShaderModule(ShaderSource src);       // multi-language bundle, §4.2
    IRhiBindGroupLayout CreateBindGroupLayout(in BindGroupLayoutDescriptor d);
    IRhiBindGroup     CreateBindGroup(in BindGroupDescriptor d);  // UBO ranges (dynamic offsets), textures
    IRhiRenderPipeline CreateRenderPipeline(in RenderPipelineDescriptor d);
    IRhiSurface       CreateSurface(in SurfaceSource src);        // HWND / X11 window / canvas id
    IRhiFramebuffer   CreateFramebuffer(in FramebufferDescriptor d); // offscreen color+depth
    IRhiQueue Queue { get; }
    RhiCommandEncoder CreateCommandEncoder();
    void BeginFrame();  // drains deferred deletions, applies upload budget
    void EndFrame();
}

public struct RenderPipelineDescriptor
{
    public IRhiShaderModule Shader;              // vertex+fragment pair
    public VertexLayout[]   VertexBuffers;       // stride, per-vertex / per-instance, attribute formats
    public PrimitiveTopology Topology;           // TriangleList; LineList only as hairline fallback
    public CullMode Cull; public FrontFace FrontFace;
    public DepthStencilState DepthStencil;       // test/write/compare, DEPTH BIAS (const + slope), stencil
    public BlendState Blend;                     // premultiplied alpha
    public TextureFormat[] ColorFormats; public TextureFormat DepthFormat;
    public IRhiBindGroupLayout[] BindGroupLayouts; // 0=frame 1=pass 2=material 3=object (dynamic offsets)
}

public sealed class RhiRenderPassEncoder
{
    public void SetPipeline(IRhiRenderPipeline p);
    public void SetBindGroup(int index, IRhiBindGroup g, ReadOnlySpan<uint> dynamicOffsets);
    public void SetVertexBuffer(int slot, IRhiBuffer b, ulong offset);
    public void SetIndexBuffer(IRhiBuffer b, IndexFormat fmt, ulong offset);
    public void DrawIndexed(int indexCount, int instances, int firstIndex, int baseVertex, int firstInstance);
    public void MultiDrawIndexed(ReadOnlySpan<DrawIndexedCmd> cmds); // GL: glMultiDrawElementsBaseVertex; WebGL: JS-side loop
    public void SetViewport(...); public void SetScissor(...);
    public void End();
}

public interface IRhiSurface { void Configure(int w, int h); RhiTextureView AcquireNext(); void Present(); }
```

Rules that keep every backend implementable:

- **Uniforms only via UBOs** (std140, 256-byte-aligned records, dynamic offsets). Supported by GL 3.3 (`glBindBufferRange`), WebGL2, WebGPU. No loose `glUniform*`.
- **WebGL2 is the feature ceiling** for the core set: no geometry shaders, no SSBOs, no compute. Instancing and `gl_VertexID` are allowed (WebGL2 has both).
- **Commands are data, not calls.** The encoder writes a compact binary command list (opcode + fixed-size args in pooled buffers). Desktop backends interpret it in C#; the WebGL backend ships the *same buffer* across the JS boundary **once per render pass**.
- **Explicit lifetime.** `Dispose` enqueues into a deferred-deletion queue drained in `BeginFrame` on the render thread. No GPU work in finalizers, ever — this deletes the `toDelete`/`FreeLists` choreography and the `Application.ApplicationExit` cleanup hooks.

### 4.2 Shader strategy

Hand-maintained **single-source GLSL**, dual-emitted; hand-ported WGSL later.

- Shaders are authored once in a GLSL "ES 3.00-compatible" subset; a preamble injector emits `#version 330 core` (desktop) or `#version 300 es` + precision qualifiers (WebGL2). At this feature level the dialects are nearly identical — a solved, boring technique.
- The whole set is small and stable, roughly **eight programs**: lit triangles, transparent triangles, screen-space stippled line segments, point symbols/billboards, glyph mesh, 2D overlay, selection halo, blit.
- When the WebGPU backend arrives, port these to WGSL by hand (days, not weeks) rather than adding a runtime cross-compiler (SPIRV-Cross/naga/tint) to the build and the WASM payload. `ShaderSource` is a `{language → source}` bundle behind one type, so an offline cross-compilation step can be added later without API change.

### 4.3 The WebGL2 backend from .NET WASM

- .NET 8/9 `browser-wasm` with `[JSImport]`. Per render pass the backend makes **one** interop call: `executor.run(commandBuffer, …)`, where the command buffer marshals as a `MemoryView` over WASM linear memory (zero copy). A small `webgl-executor.js` (~500 lines) interprets opcodes into WebGL2 calls. Resources are small integer handles indexing JS-side arrays — no object marshalling anywhere.
- Bulk uploads (`Queue.WriteBuffer`) pass the same way; `gl.bufferData` accepts typed-array views over the WASM heap directly. A 10 MB vertex page upload is one call.
- Evaluated alternative: emscripten GLES→WebGL static linking (the Uno/Avalonia-Skia route). Rejected as baseline — it drags in emscripten EGL emulation and native relinking for little gain since all GL usage is already funneled through one thin backend. Documented fallback if interop throughput surprises (spike S1 measures this before anything depends on it).
- Hosting: nothing depends on Blazor. A plain `browser-wasm` sample host owns the page; under Blazor/Avalonia-browser/Uno the executor loads as a JS module and the loop hooks `requestAnimationFrame`.
- .NET WASM is effectively single-threaded today; the scene layer tolerates that (time-sliced tessellation, coarse-LOD-first policy, §5.7). Context loss (`webglcontextlost`) surfaces as `DeviceLost` → full GPU-residency rebuild from CPU-side caches (which are kept anyway).

### 4.4 DirectX on Windows

The maintainers prefer DirectX on Windows. The architecture accommodates this without giving up the GL backend's other roles (Linux desktop; dialect-sibling of the WebGL2 browser path). Three routes, in order of availability:

1. **ANGLE as the default Windows runtime (Phase 1, near-zero extra code).** [ANGLE](https://chromium.googlesource.com/angle/angle) implements OpenGL ES on top of **Direct3D 11**; it is the GL implementation inside Chrome, Edge and Firefox on Windows, i.e. the most battle-tested GL-on-Windows stack in existence. Our GL backend targets exactly the GLES3-compatible subset ANGLE serves. Shipping `libEGL.dll`/`libGLESv2.dll` (~a few MB) and creating the context via EGL instead of WGL (comparable glue, ~200 lines) means **all Windows rendering executes on D3D11 from day one** — while the backend code stays identical to Linux. This directly retires the failure class that motivates preferring DirectX on Windows: `wglCreateContext` failures in long-running sessions and docking scenarios (issue #37), Remote Desktop sessions without usable vendor GL, and unreliable OEM GL drivers on office/Intel hardware. Raw vendor GL remains selectable (setting/fallback), and spike S4 is thereby promoted from "sanity check" to qualifying the default.
2. **Native D3D11 backend (`CADability.Rendering.D3D11`, additive, M effort).** Because the RHI follows WebGPU conventions — immutable pipeline-state objects, constant-buffer-style UBOs with dynamic offsets, 0‥1 depth range — D3D11 (feature level 11_0) is nearly congruent with it; it is the GL backend that does the adapting, not the D3D one. Silk.NET.Direct3D11 keeps the binding ecosystem consistent; DXGI flip-model swapchains replace everything WGL did badly (no pixel-format descriptors, no `wglShareLists`, clean multi-window, real vsync control, composition-friendly for future WPF/WinUI hosts). Cost: HLSL ports of the ~8 shader programs (hand-maintained, exactly like the planned WGSL port) and a loop-emulation of `MultiDrawIndexed` (same as the WebGL2 executor does). Worth building when native D3D interop or PIX-based tooling matters; decided after Phase 2.
3. **WebGPU backend (already roadmapped)** — `wgpu-native` runs on **D3D12** on Windows; one modern API and shader language for desktop and browser alike.

Recommended sequencing: route 1 as the Windows default at Phase 1; choose between routes 2 and 3 after Phase 2 based on profiling and interop needs — both are additive backends behind the same RHI, neither blocks the other.

### 4.5 Contexts and surfaces

Replaces the process-static WGL sharing and the hidden bitmap contexts:

- **Desktop GL:** one `IRhiDevice` = one GL context created against a hidden window. Each `IRhiSurface` wraps a native drawable (HDC/X11 window); `AcquireNext` = make-current on that drawable with the shared context. All resources live in one context, so multiple canvases (several ModelViews, LayoutView, AnimatedView) share meshes for free — the goal `wglShareLists` chased, without the fragility (issues #37/#62).
- **Offscreen:** `IRhiFramebuffer` + `ReadPixels` → `CpuImage`, serving LayoutView's print rasterization and thumbnails; replaces `IUIService.CreatePaintInterface(Bitmap, precision)`'s special bitmap GL contexts.
- **Browser:** one canvas = one WebGL2 context = one device (a WebGL constraint; WebGPU later removes it).

---

## 5. The retained scene layer

This is the heart of the redesign — the part PR #339 did not attempt.

### 5.1 Core types

```csharp
// CPU side, produced by tessellation workers; arrays from ArrayPool (PR #340's churn lesson)
public sealed class CpuMesh
{
    public float[] Interleaved;   // pos.xyz + normal.xyz (+ uv where textured), float32
    public int[]   Indices;       // ushort emitted where possible
    public BoundingCube Bounds;
    public double Precision;      // which LOD level this is
}

// GPU residency: sub-allocation from large pooled pages (8–16 MB vertex/index pages)
public readonly struct MeshHandle { int Page; int VertexOffset; int VertexCount; int IndexOffset; int IndexCount; }

public sealed class RenderItem
{
    public uint Id;               // == IGeoObject.UniqueId → picking/selection mapping
    public MeshHandle[] Lods;     // up to 3 levels, coarse→fine (slot null = not built yet)
    public ushort MaterialId;     // index into the material table (64-byte std140 records, one UBO)
    public ushort TransformId;    // 0 = static/world-baked; else transform-node index
    public ushort LayerId;
    public ItemKind Kind;         // Face, EdgeCurve, Curve, Text, PointSymbol, Bitmap, Unscaled
    public ItemFlags Flags;       // Visible, Transparent, ZoomIndependent, …
    public BoundingCube WorldBounds; // mirrored into SoA arrays for the cull loop
}
```

- **Materials as data:** color, edge color, transparency, line width/pattern live in a table of 64-byte records inside one dynamic-offset UBO. *A recolor writes 64 bytes; geometry is untouched.*
- **Double precision:** CAD extents exceed float32. Standard relative-to-center technique: each geometry page carries a double-precision origin; vertices are float32 relative to it; the view matrix is composed per page in double on the CPU and uploaded as float32.

### 5.2 Mapping kernel objects to items

| Kernel object | Render items |
|---|---|
| `Face` | one triangle item (`GetTriangulation` + cached normals from PR #340) + edge polyline items (deduplicated per shell, so shared edges upload once) |
| Curves (`Line`, `Ellipse`, `BSpline`, `Path`, `Polyline`) | polyline items tessellated at LOD precision, cumulative arclength recorded for stipple |
| `Text` | per-glyph mesh instances (§6.4) |
| `Point` / handles | point-symbol instances |
| Bitmaps / icons | textured quad items |
| Unscaled objects | items flagged `ZoomIndependent`, scaled in the shader (§6.3) — removes the per-zoom rebuild at `ProjectedModel.cs:1336-1352` |
| `Block` / `BlockRef` / `IDrive` groups | transform nodes or instanced draws (§5.5) |

### 5.3 Batching for millions of triangles

- **Geometry pooling:** all meshes of a model share vertex/index pages per vertex format; static top-level geometry is baked in (page-relative) world space so `TransformId = 0` for most items.
- **Draw batching:** `ViewScene` maintains a cached draw list sorted by (pass, pipeline, page, material). Runs sharing all four merge into one draw; runs differing only in material become a `MultiDrawIndexed` span with per-draw dynamic UBO offsets. Desktop GL executes `glMultiDrawElementsBaseVertex`; WebGL2 loops inside the JS executor — still one interop call. Target: a few hundred to ~2,000 draws per frame for millions of triangles across thousands of faces.
- **Instancing for repeats:** `BlockRef`s sharing a `Block`, repeated point symbols and glyphs render as instanced draws (per-instance mat3x4 + material index, step-mode Instance). Fully supported on WebGL2.
- Explicitly out of scope for v1: bindless/GPU-driven indirect pipelines (unavailable on WebGL2, unnecessary below ~10M triangles).

### 5.4 Per-object dirty tracking

A `SceneSync` object per (Model, SceneGraph) subscribes to `GeoObjectDidChangeEvent` and classifies with the already-computed `GeoObjectChange.OnlyAttributeChanged`:

| Change | Action | Cost |
|---|---|---|
| Attribute-only (color, layer, width) | update the item's material record / bucket | O(1): a 64-byte UBO write |
| Geometry | mark that object's items stale, free pool ranges (deferred), enqueue re-tessellation of **that object**, show coarse LOD first | O(object) |
| Add / Remove | create / destroy items | O(object) |

View state (visible layers, selection, hover) lives in `ViewScene` as filters over the shared scene — a layer toggle re-filters the draw list and touches nothing else. This replaces the model-global `displayListsDirty` (`Model.cs:1656-1661`) and the per-view list re-collection in `ProjectedModel.Paint`.

### 5.5 Transforms

One flat transform-node table (mat4 array in a dynamic-offset UBO) — deliberately not a general scene tree:

- Static geometry: node 0 (identity; baked into page space).
- Unique `Block` insertions: one node per insertion; repeated insertions use instancing instead.
- `AnimatedView` drives: exactly its current proven design — display lists per `IDrive` replayed under `PushMultModOp` (`AnimatedView.cs:794-831`) — maps to **one node per drive**; animation updates matrices only. AnimatedView becomes the *easiest* view to migrate, not the hardest.

### 5.6 Culling and LOD

- **Frustum culling:** item world-AABBs in SoA arrays, linear plane test per frame. At 10⁴–10⁵ items this is well under a millisecond and trivially correct. The kernel octree stays pick-only (different granularity, different lifetime). A seam (`ICullingIndex`) allows a render-side BVH later if profiling ever demands it.
- **Discrete LOD, 2–3 levels**, quantized from the existing precision machinery: L0 ≈ `Extent.MaxSide/500` (tiny, always resident), L1 ≈ `/5,000`, L2 ≈ `/50,000` (capped like today's `HighestDisplayPrecision`). Each visible item picks a level by projected screen size. Zoom-in requests L2 builds only for faces that are actually large on screen; zoom-out switches indices to the resident L0 — no re-tessellation storm, by construction (the failure class PR #340 patches around). L2 lives under a memory budget (e.g. 512 MB) with LRU eviction to L1. `Face.AssureTriangles`' "2× finer" heuristic becomes an exact per-level slot.

### 5.7 Threading and lifetime

- **Tessellation workers:** `Task`-based with per-object `CancellationToken` — replaces the hand-rolled manage-thread + `runningThreads` set + `EventWaitHandle` machinery (`Model.cs:130-352`). Workers produce `CpuMesh` into a completion queue; arrays return to the pool after upload.
- **Render thread owns all GPU work.** On WinForms initially the UI thread (paint in `OnPaint`, bisectable behavior), optionally a dedicated thread later; on WASM the `requestAnimationFrame` callback (single-threaded: tessellation runs time-sliced; coarse-first LOD keeps interaction live).
- **Uploads** drain under a per-frame byte budget (e.g. 8 MB) to avoid hitches.
- **Deletion** is generation-counted and deferred to `BeginFrame`. No finalizers touch GPU state.

---

## 6. CAD-specific features, technique by technique

1. **Edges over faces** → depth bias in `DepthStencilState` (constant + slope-scale; `glPolygonOffset` / WebGL `polygonOffset` / WebGPU `depthBias`). Since patterned lines are triangles (below), `GL_POLYGON_OFFSET_FILL` covers everything. Retires the matrix-nudge trick behind `PaintFaces`/`FacesBehindEdgesOffset` and its choreography in `ProjectedModel.Paint`.
2. **Line width & stipple** (WebGL2 has no wide lines) → **geometry-less instanced segment quads**: a static unit quad, per-instance `(posA, posB, arclenA, arclenB)`; the vertex shader expands perpendicular in NDC by `widthPx × pixelToNdc`; the fragment shader applies the `LinePattern` (up to 8 stroke/gap pairs in the material record) over interpolated arclength — world-space patterned for print fidelity. CPU-expanded strips were rejected (2–4× memory, re-expansion on width change). Width-1 hairlines may fall back to native `LineList`.
3. **Zoom-independent symbols/handles** → instanced billboards scaled by `pixelToWorld` from the frame UBO. No rebuild on zoom; retires the `ZoomIndependentDisplayList` capability dance.
4. **Text** → glyph outlines tessellated via the managed [Typography](https://github.com/LayoutFarm/Typography) library (pure C#, WASM-safe) into per-(font, glyph) meshes cached at em scale, instanced per character. Same vector fidelity as today's `wglUseFontOutlines`, but portable and print-true. SDF atlases were evaluated: better for tiny rotating HUD text, worse for print — optional later addition, not the base. Font resolution: platform font directories on desktop, bundled fonts + app resolver hook on web.
5. **Transparency** → sorted back-to-front per transparent item (centroid depth, depth-write off) after the opaque pass — already better than today's unsorted blend. Weighted-blended OIT documented as a flag-gated upgrade (needs `EXT_color_buffer_float` on WebGL2).
6. **Selection & hover without churn** → `ViewScene` holds selected/hovered id sets; an overlay pass re-draws exactly those items with a highlight pipeline (stencil-outline halo: pass A writes stencil, pass B redraws expanded by N pixels, masked). Zero geometry rebuild on hover; fixes issue #308's per-frame list creation. The classic "wobble" can be emulated (K offset redraws) behind a setting.
7. **Grid / rubber band / action feedback** → `FrameComposer` overlay stages mapped 1:1 from the existing `PaintBuffer.DrawingAspect` events (Background, Drawing, Select, Active), fed through a small `IOverlayPainter` (immediate-style lines/rects/points/text) into per-frame dynamic buffers — chatty is fine here, the data is tiny.
8. **Picking** → unchanged CPU path (`Model.GetObjectsFromRect` + `Projection.PickArea` over the octree) — portable everywhere. Optional later: GPU id-buffer picking (items render `Id` into an R32Uint target; async readback) — noted, not scheduled; WebGL2 readback stalls.
9. **Printing/export** → `PrintToGDI` keeps consuming `IPaintTo3D` unchanged (it never touched GL) and moves to `CADability.Forms` when the core drops System.Drawing; `PaintToSTL` and the WebGL/three.js exporters later read the `TessellationCache` through a new `IMeshSource` interface — same triangles, less ceremony.
10. **GDI2DView** → survives untouched on the WinForms host (it never used GL); long-term it can be subsumed by the new renderer's 2D/ortho mode — a maintainer decision, not a technical necessity.
11. **Offscreen bitmaps** (LayoutView print rasterization, thumbnails) → framebuffer render + `ReadPixels`, replacing the special bitmap GL contexts.

---

## 7. Compatibility and migration

### 7.1 The `PaintTo3DToScene` adapter

A complete `IPaintTo3D` implementation over the new engine, so every existing view and sink works unchanged while the migration proceeds:

- State setters (`SetColor/SetLineWidth/SetLinePattern/PushMultModOp/PushState/…`) drive a recording cursor (current material key + transform stack).
- Primitives (`Triangle`, `Polyline`, `Points`, `Arc` — arcs recorded analytically and tessellated at `Precision`) append into `MeshBuilder`s.
- `OpenList`/`CloseList` produce a `RetainedGroup : IPaintTo3DList`; `MakeList` composes groups; `List(g)` marks a group live in the current frame; `SelectedList(g, wobble)` marks it live with highlight; `FreeUnusedLists` disposes.
- 2D members (`Line2D`, `FillRect2D`, …) route to the overlay batch; `SetProjection/Clear/FinishPaint` map to frame begin/present.
- Reports `CanDoArcs | ZoomIndependentDisplayList`, so existing call sites take their current branches.

Because `ModelView.Paint`, `ProjectedModel.Paint`, `AnimatedView`, `LayoutView` and `SelectObjectsAction` only ever talk to `ICanvas.PaintTo3D`, swapping the object behind that property — one new branch in `CadCanvas.ShowView` (`CadCanvas.cs:128`) — migrates them wholesale with **zero view-code changes**. Even adapter-only rendering already removes GL 1.1, per-character display lists, and the context statics; the model-global rebuild cadence remains until Phase 2 replaces it.

### 7.2 Phased roadmap (every phase leaves the repo shippable)

**Phase 0 — Groundwork (S)**
Merge PR #340 (cached normals + tamed re-triangulation are prerequisites). Add the `WebDebug`/WEBASSEMBLY configuration to CI so the System.Drawing-free compile path stops rotting. Multi-target the core `netstandard2.0;net8.0`.
*Exit: green CI on all configurations; #340 merged.*

**De-risking spikes (S each, parallel, throwaway code)**
- **S1 — WASM interop throughput** (the riskiest assumption, measured first): net8-browser page issuing ~100k batched WebGL2 commands + ~50 MB uploads through one-call-per-pass interop.
- **S2 — Multi-canvas shared GL context** across two WinForms controls + one X11 window via Silk.NET (harvests PR #339's `WglContext` learnings).
- **S3 — Typography glyph tessellation** vs. current text output (screen + print comparison).
- **S4 — ANGLE qualification**: run the GL backend on ANGLE (D3D11) to catch UBO/instancing edge cases early. ANGLE is both WebGL's usual Windows substrate *and* the intended default Windows runtime (§4.4), so this spike qualifies the day-one DirectX path.

**Phase 1 — Rendering core + desktop GL + adapter (L)**
New projects `CADability.Rendering` + `.OpenGL`: RHI, shader set, geometry pools, materials, FrameComposer, `PaintTo3DToScene`. On Windows the backend runs over ANGLE by default (Direct3D 11 underneath, §4.4); raw GL selectable. Opt-in via a new `IView.PaintType` value (e.g. `"3D-RHI"`) or a setting; legacy remains default. Golden-image comparison tests introduced here.
*Exit: ModelView + SelectObjectsAction + AnimatedView at visual parity on sample models, Windows (on ANGLE/D3D11) + Linux.*

**Phase 2 — Native scene path for ModelView (L)**
`SceneSync` per-object dirty tracking, `TessellationCache` with LOD levels, culling, transform nodes, id-based selection/hover, sorted transparency. ModelView's paint bypasses `ProjectedModel.Paint`/display lists **when the canvas supplies an RHI device**; the classic `IPaintTo3D`-driven path stays intact and supported for overlays, legacy sinks and third-party engines (§7.4).
*Exit (measured): 1M-triangle model orbits at 60 fps; single-object recolor causes zero re-tessellation/re-upload (asserted); hover allocates nothing per frame; issue #308 scenarios fixed. New path becomes the desktop default; legacy still selectable.*

**Phase 3 — System.Drawing detox of the core (M; parallel with 1–2, different files)**
Promote the `WebDrawing.cs` value types to the single always-compiled type system (working name `CADability.Media`; keep the current namespace initially to minimize diff). ~10 small mechanical PRs across the ~98 affected files; delete the `#if WEBASSEMBLY` forks (69 files); drop `System.Drawing.Common` and `Gdi.cs` from the core; move `PrintToGDI` to `CADability.Forms`; conversion helpers live in Forms.
*Exit: core has no System.Drawing.Common reference; the WebDebug configuration is deleted (one build flavor again); the public `IPaintTo3D` surface is preserved per the §7.4 contract (System.Drawing.Primitives value types unchanged; `Bitmap`/`FontStyle` members kept compiling on desktop targets).*

**Phase 4 — Browser backend + sample host (M–L; needs Phases 1 & 3 + spike S1)**
`CADability.Rendering.WebGL`, `CADability.Host.Browser` (canvas, DOM events → Substitutes, RAF loop), bundled fonts. WebGPU backend optional afterwards (M), reusing the executor scheme + wgpu-native on desktop.
*Exit: a sample model loads, orbits, picks and selects in Chrome/Firefox at interactive rates; WASM build in CI.*

**Phase 5 — Legacy deletion (M, mechanical but wide)**
LayoutView's offscreen path moves to framebuffers; remaining views default to the new stack; delete `CADability/OpenGL.cs` (51,940 lines), `CADability/Gdi.cs`, `CADability.Forms/PaintToOpenGL.cs` and the GL branches of `CadCanvas`.
*Exit: no P/Invoke in the core. `IPaintTo3D` implementations remaining: the adapter, `PaintToGDI`, `PrintToGDI` (both in Forms), `PaintToSTL`, plus any third-party engines — the interface and the classic model-traversal path remain supported per the §7.4 contract.*

**Parallelization:** Phase 3 runs alongside 1–2; spikes run up front; WebGPU and the Avalonia host are independent follow-ons. Every phase decomposes into reviewable PRs.

### 7.3 Main risks and mitigations

| Risk | Mitigation |
|---|---|
| WASM interop throughput insufficient | Spike S1 **before** anything depends on it; emscripten static-link fallback documented (§4.3) |
| Visual parity subtleties (offset scale, line joins, text metrics) | Golden-image tests from Phase 1, per backend |
| Tessellation threads vs. non-thread-safe kernel surfaces | Keep the existing per-object lock discipline (`Face` triangulation locks); single-writer cache design |
| Driver/ANGLE quirks | Spike S4; golden images run per backend |
| WebGL context loss | CPU-side caches retained → full residency rebuild on restore |
| Memory growth from LOD levels | L0 tiny + always resident; L2 under budget with LRU eviction |
| Community bandwidth | Strict phase independence; legacy path stays default until Phase 2's measured exit criteria |

### 7.4 External `IPaintTo3D` implementations — the compatibility contract

At least one downstream customer has built **their own rendering engine implementing `IPaintTo3D`** and drives CADability's model through it in production. That makes `IPaintTo3D` a de-facto rendering SPI, not an internal seam, and this proposal treats it accordingly. The commitment:

1. **The interface stays public and supported — it is not deprecated by this proposal.** Phase 5 deletes the *OpenGL 1.1 implementation* (`PaintToOpenGL`, `OpenGL.cs`), never the interface.
2. **CADability keeps driving arbitrary implementations.** Two things make a third-party engine work today, and both are preserved:
   - the *emission side*: every `IGeoObject.PaintTo3D(IPaintTo3D)` implementation — which the new architecture needs anyway (the `PaintTo3DToScene` adapter, `PaintToSTL`, `PrintToGDI` and custom third-party GeoObjects all record through it);
   - the *driving side*: a supported traversal that paints a `Model`/view into any `IPaintTo3D` (today `Model.RecalcDisplayLists` + `ProjectedModel.Paint` via `ICanvas.PaintTo3D`). When Phase 2 gives the built-in views a native scene path, this classic path **remains available**: a view whose canvas supplies a classic `IPaintTo3D` (rather than an RHI device) continues to paint exactly as today. If the display-list plumbing is ever simplified, an equivalent public driver (e.g. `PaintModelTo(IPaintTo3D, view parameters)`) replaces it before anything is removed.
3. **Semantics stay honored.** `PaintCapabilities` negotiation, display-list behavior (`OpenList`/`CloseList`/`MakeList`/`List`), the `ZoomIndependentDisplayList` contract and the `PrePaintTo3D`/`PaintTo3DList` traversal order remain as-is for classic-path consumers. Behavioral changes on this path are bug fixes only.
4. **Signature stability across the System.Drawing detox (Phase 3).** This is the one place where care is genuinely needed, because the interface is typed against `System.Drawing`:
   - `Color`, `Point`, `PointF`, `Rectangle`, `RectangleF`, `Size(F)` come from **System.Drawing.Primitives**, which is cross-platform (WASM included) and can stay in the public interface **unchanged** — zero impact on existing implementations. The detox targets `Bitmap`/`Graphics`/GDI usage, not these value types.
   - Only members using `Bitmap` and `FontStyle` (`PrepareBitmap`, `PrepareIcon`, `DisplayBitmap`, `DisplayIcon`, `PrepareText`, `Text`) touch Windows-only types. Recommendation: **freeze `IPaintTo3D` v1 exactly as-is** and keep it compiling on desktop targets; introduce any modernized variants *additively* (e.g. overloads or a small `IPaintTo3D2` taking an `IImageSource`/font-descriptor abstraction), with default-implemented bridging so existing engines compile and run untouched. A hard signature break is reserved for an explicit major version (2.0), with a published migration guide — and only if the maintainers decide the added cleanliness is worth it (open question 5).
5. **The new stack is an offer, not an obligation, for such customers.** A third-party engine can stay on the classic path indefinitely — or adopt pieces at its own pace: `IMeshSource` (direct access to cached tessellations, no paint traversal needed), the RHI (their engine as a new backend), or the retained scene with their own passes. Each is strictly more data than `IPaintTo3D` gives them today (bulk indexed meshes instead of per-primitive calls).

Net effect for a downstream engine through the phases: Phases 0–2 change nothing (classic path untouched and initially default); Phase 3 changes nothing if the freeze recommendation is followed (Primitives types unchanged; `Bitmap`/`FontStyle` members kept on desktop); Phase 4 is irrelevant to them; Phase 5 removes only CADability's *own* GL implementation. The scenario "customer's engine breaks" is confined to an opt-in 2.0 signature modernization that may never be needed.

---

## 8. Open questions for the maintainers

1. **Core TFM policy:** keep `netstandard2.0` in the multi-target (are there .NET Framework 4.8 NuGet consumers?), or move to net8.0-only and unlock Span-based kernel APIs?
2. **Windows end-state and macOS stance:** Windows ships DirectX-backed from day one via ANGLE/D3D11 (maintainer preference, §4.4); the open part is the *end-state* — stay on ANGLE, add the native D3D11 backend (route 2), or land WebGPU/D3D12 (route 3)? Related: macOS deprecates OpenGL (4.1 max) — if macOS matters, ANGLE's Metal backend or WebGPU covers it; decide priority.
3. **Host order:** Avalonia host (issue #256's ask) before or after the browser host? The architecture supports either; browser-first exercises more constraints.
4. **GDI2DView / PrintToGDI long-term:** keep indefinitely on the WinForms host (recommended for plotter fidelity), or retire after the new renderer demonstrates 2D/vector-print parity?
5. **`IPaintTo3D` type modernization in a future 2.0:** external implementers *do* exist (§7.4), so the interface stays supported as-is. The remaining question is only whether a major version ever replaces the `Bitmap`/`FontStyle`-typed members with portable abstractions (with migration guide + shims), or whether the additive-overload route (§7.4.4) is permanent. Worth a call for feedback in #256 to find all downstream implementers.
6. **Selection visual:** replace the wobble with the stencil halo outright, or keep wobble emulation behind a setting?
7. **Assembly signing:** do the new `CADability.Rendering.*` packages sign with the existing `CADabilityKey.snk`?

---

## Appendix A — Key source anchors

| Area | Location |
|---|---|
| `IPaintTo3D` / `IPaintTo3DList` / `PaintCapabilities` | `CADability/PaintToOpenGl.cs:58-397, 582-587, 33-51` |
| Legacy GL backend | `CADability.Forms/PaintToOpenGL.cs` (2,278 lines; `OpenGlList` at 2135-2277) |
| Hand-written GL binding (to delete) | `CADability/OpenGL.cs` (51,940 lines), `CADability/Gdi.cs` |
| Display-list ownership + global dirty flag + background recalc | `CADability/Model.cs:118-134, 269-352, 1656-1670` |
| Per-view replay loop | `CADability/ProjectedModel.cs:1290-1368` |
| Paint cycle, `ICanvas`, `IView` | `CADability/ModelView.cs:41-140, 767-834` |
| Backend selection seam | `CADability.Forms/CadCanvas.cs:121-151` |
| Face tessellation cache | `CADability/Face.cs:5764-5770, 6855-6923, 8624` |
| Retained-rendering precedent | `CADability/AnimatedView.cs:49, 325-342, 794-831` |
| Selection/hover churn | `CADability/SelectObjectsAction.cs:474-542` (issue #308) |
| Portability seeds | `CADability/WebDrawing.cs`, `CADability/FormsSubst.cs`, `#if WEBASSEMBLY` (69 files) |
| Mesh-data readiness proof | `CADability/ExportToThreeJs.cs:337` |
