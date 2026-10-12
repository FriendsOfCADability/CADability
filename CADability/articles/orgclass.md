# Organizational or root classes of CADability

### IFrame and FrameImpl
The name and idea for this class resembles the MFC CFrameWnd class, which was long time ago used by the predecessor of CADability.

While [IFrame](xref:CADability.IFrame) is an interface which is always used when we have a reference to the frame, [FrameImpl](xref:CADability.FrameImpl) 
is an implementation of that interface. And by now the only implementation. The Windows.Forms dependent parts are implemented in [CadFrame](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadFrame.html).
The idea was that you could also have different implementations of IFrame for multiple documents or multiple concurrent views, but it seems, this will not be necessary. But for different
frameworks (besides Windows.Forms) you can very well write your own implementation analogous to CadFrame.

The frame gives you access to the 
- [IControlCenter](xref:CADability.UserInterface.IControlCenter) (the abstraction of a properties explorer or property grid, which is implemented in CADability.Forms)
- currently open [Project](xref:CADability.Project)
- the active [View](xref:CADability.IView) on the project and the list of available views
- the [GlobalSettings](xref:CADability.Settings.GlobalSettings)
- the [ActionStack](xref:CADability.ActionStack) and the currently running [Action](xref:CADability.Actions.Action)
- the [UIService](xref:CADability.UserInterface.IUIService), which gives you access to user interface stuff like showing a standard dialog or getting the mouse position

Furthermore the frame handles most of the global menus, like opening a file or drawing a line. It is also the place where you can set (i.e. start) an [Action](xref:CADability.Actions.Action).

The CadFrame implements the still abstract class IFrameImpl, by implementing those things, you cannot do in .NET core.

### IView and the views which implement it
[IView](xref:CADability.IFrame) is the interface, which must be implemented by all kind of views.

A view receives the mouse-, scroll-, drag and drop- and paint-messages, typically but not necessarily from a Windows.Forms.Control and uses the service from an ICanvas implementing object
to interact with that control. In CADability.Forms there is an implementation of ICanvas, namely [CadCanvas](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html), 
which is a Windows.Forms.Control and represents the view onto a 3d [Model](xref:CADability.Model).

There are different types of views:
- [ModelView](xref:CADability.ModelView) is the normal view. It can handle [Action](xref:CADability.Actions.Action)s to select objects and do 3d modeling. It typically 
but not necessarily is displayed via OpenGL (see [CadCanvas](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html)).
- [GDI2DView](xref:CADability.GDI2DView) a view, which uses GDI instead of OpenGL for it's display. This is actually handled 
in [CadCanvas](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html)
- [LayoutView](xref:CADability.LayoutView) is used to compose a page from different projections of a model in different sections of the paper. It is used for printing.
- [AnimatedView](xref:CADability.AnimatedView) is used to show different parts of the 3d model in movement. It can be used to define the dependencies of mechanical objects to 
each other (the movement axis). Usually the time schedule of this movement is provided from outside, but it can be defined in the AnimatedView.

### ICanvas and CadCanvas
[ICanvas](xref:CADability.ICanvas) is the abstraction of a service used by CADability to display a model. [CadCanvas](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html)
is a Windows.Forms.Control based implementation. [ICanvas](xref:CADability.ICanvas) and [IView](xref:CADability.IView) are working closely together. While the canvas forwards 
the typical events like mouse-, scroll-, drag and drop- and paint-events to the view, the view uses the canvas to display the effects of these events. You can implement a different
version of CadCanvas, which is based on another framework (i.e. not Windows.Forms), and the community would be happy if you would contribute it.

**OpenGL**: the current implementation of CadCanvas in CADability.Forms provides a "paint-interface" [IPaintTo3D](xref:CADability.IPaintTo3D) which is implemented in a quite old 
fashioned OpenGL style in the class [PaintToOpenGL](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.PaintToOpenGL.html). If someone feels called to provide a better implementation based 
on something else (maybe WebGPU or a newer OpenGL approach), please go ahead.

### IUIService, the abstraction of some system tasks
[IUIService](xref:CADability.UserInterface.IUIService) is implemented in [CadFrame](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadFrame.html). It handles some tasks, which 
are usually provided by the system, and have no place in the CADability kernel: showing standard dialogs, handling clipboard, getting the mouse position on the screen. In a different environment
you will have to write your own implementation.

### MenuManager: where do the menus come from
The classes [MenuManager](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.MenuManager.html), [ContextMenuWithHandler](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.ContextMenuWithHandler.html) and
[MenuItemWithHandler](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.MenuItemWithHandler.html) implement the handling of the menus on the Windows.Forms side. the classes 
[MenuResource](xref:CADability.UserInterface.MenuResource) and [MenuWithHandler](xref:CADability.UserInterface.MenuWithHandler) and the interface [ICommandHandler](xref:CADability.UserInterface.ICommandHandler)
implement the menu structure and handling in the CADability kernel. The menu structure is implemented in the CADability kernel, but may be modified by the user software. A menu item has
a unique id, which is a string, typically in the form of "MenuId.Edit.Cut" or "MenuId.Constr.Circle.CenterRadius". Most menus are loaded from _MenuResource.xml_, but may also be composed by code.
- [MenuManager](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.MenuManager.html): create Windows.Forms.MainMenu and Windows.Forms.ContexMenu from [MenuWithHandler](xref:CADability.UserInterface.MenuWithHandler)[] 
via static methods
- [ContextMenuWithHandler](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.ContextMenuWithHandler.html): a Windows.Forms.ContextMenu, which forwards the menu selection to the [ICommandHandler](xref:CADability.UserInterface.ICommandHandler)
- [MenuItemWithHandler](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.MenuItemWithHandler.html): a Windows.Forms.MenuItem, which forwards the clicking on the menu to the [ICommandHandler](xref:CADability.UserInterface.ICommandHandler)
- [MenuResource](xref:CADability.UserInterface.MenuResource): a collection of static methods to load menus from _MenuResource.xml_.
- [MenuWithHandler](xref:CADability.UserInterface.MenuWithHandler): a simple structure consisting mostly of a few strings that describe a menu entry in the CADability kernel. It also may
have sub-entries in the form of an MenuWithHandler[], allowing a menu hierarchy. It also knows the target [ICommandHandler](xref:CADability.UserInterface.ICommandHandler) to call, when it gets clicked.
- [ICommandHandler](xref:CADability.UserInterface.ICommandHandler): an interface implemented by many objects to handle menu clicks.
- **MenuResource.xml** is an XML file describing menu structures. It is embedded in the CADability kernel as a resource, but my be substituted by a user provided XML file. It is language neutral.
- **StringTableXxx.xml** is an XML file providing language dependent strings to language independent IDs (which are also strings). The class to access these strings is [StringTable](xref:CADability.UserInterface.StringTable).
Currently there exist two language versions of this file, namely English and German. You are welcome to provide more languages and share it with the community.

### Toolbars vs. Menus
It is only a matter of your main form to also show the menus or parts of the menus as tool-bars. There is no concept of tool-bars in the CADability kernel. There is the class [ToolBars](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadFrame.html) implemented
based on Windows.Forms.ToolStrip in CADability.Forms, which provides a simple implementation for tool-bars. If this doesn't meet your requirements, go ahead and implement your own
or enhance the implementation, and let the community know!

### IControlCenter, a properties explorer or property grid 
The property grid is of course a framework dependent item, which is implemented in CADability.Forms with the class 
[PropertiesExplorer](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.PropertiesExplorer.html) and displayed in the [CadFrame](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadFrame.html).
It shows properties of the objects of the CADability kernel, but may also show the properties of your objects, if they
implement the [IPropertyEntry](xref:CADability.UserInterface.IPropertyEntry) interface.
You can write your own version of PropertiesExplorer if you don't like the visual style or want to use a different framework. [IControlCenter](xref:CADability.UserInterface.IControlCenter) 
is the interface by which the CADability kernel accesses the property grid. (In a previous version the properties explorer was called ControlCenter).

### Actions: how to implement the interaction between the user and CADability
You can use the CADability kernel as a database of (3d) geometrical objects, analyze and modify them with your program code, retrieve and store them into files. But you can
also write code to create and manipulate these objects interactively with mouse and keyboard. the class [Action](xref:CADability.Actions.Action) is the base class of all actions, 
the class [ConstructAction](xref:CADability.Actions.ConstructAction) provides a higher level of access like specifying a 3d point with the mouse or selecting parts of the model.
All interactive means in CADability are implemented via Action. There is a not so much used concept of an [ActionStack](xref:CADability.ActionStack), which means that
while an action is running there might be another action activated, which upon completion returns to the previous action. this is used for intermediate construction, but rarely
used by other application code.



 