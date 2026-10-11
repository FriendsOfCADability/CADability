# Introduction to CADability
The CADability solution is composed of two class libraries and an application:
- CADability, a dll, which contains all the classes of the geometrical objects, the action classes, the structure (but not the graphical implementation) of 
the user interface and some organizational classes.
- CADability.Forms, a dll, which contains the graphical implementation of the user interface based on Windows.Forms and other windows specific functions
- CADability.App, an exe, which is a very thin container of CADability.Forms

For your first experience you can simply build and start the solution and try to draw or construct 3d objects. Typically you will replace the CADability.App 
by your own application or use only the CADability.dll to analyze some 3d models.

Here is a quick overview of the [organizational classes](orgclass.md)

This is an overview of the CAD [database](database.md), the geometrical entities that make up a model.

And this is the [API documentation](../api/index.md).
