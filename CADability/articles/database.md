# Introduction to CADability's data model

Geometric entities are the main objects in the CADability database.

Usually many geometric entities are contained in a [Model](xref:CADability.Model). If a model is displayed in a [ModelView](xref:CADability.ModelView) the geometric entities 
are displayed on the screen (in a [CadCanvas](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadCanvas.html) inside a [CadFrame](https://friendsofcadability.github.io/CADability/CADabilityFormsDoc/api/CADability.Forms.CadFrame.html)). 

The entity that is normally saved in a file is the [Project](xref:CADability.Project). The project contains one or more models and some other information, like lists of attributes.

## What are geometric entities?
There are some families of geometric entities:
- Curves (e.g. [Line](xref:CADability.GeoObject.Line), [BSpline](xref:CADability.GeoObject.BSpline), [Ellipse](xref:CADability.GeoObject.Ellipse) etc., in a 3d coordinate space)
- [Block](xref:CADability.GeoObject.Block)s (collections of geometric entities, hierarchical tree of objects)
- BRep objects (e.g. [Solid](xref:CADability.GeoObject.Solid), [Face](xref:CADability.GeoObject.Face), etc.) (see [Wikipedia](https://en.wikipedia.org/wiki/Boundary_representation))
- Text, Dimension, Hatches

Geometric entities can be created and manipulated both interactively and by program code.

Geometric entities are serializable as JSON files. A [Model](xref:CADability.Model) can be exported to or imported from a STEP file.

## What do geometric entities have in common?
From a programmer's point of view it is the [IGeoObject](xref:CADability.GeoObject.IGeoObject) interface.

Geometric entities may be
- visualized (in a [Model](xref:CADability.Model) which is contained in a [ModelView](xref:CADability.ModelView))
- serialized in a JSON file
- selected with mouse-clicks in various ways
- modified by a [ModOp](xref:CADability.ModOp) (i.e. moved, rotated scaled, reflected etc.)
- displayed in a property grid, where their properties are editable
- interactively created or modified by various [Action](xref:CADability.Actions.Action)s.

## How do I connect additional information to geometric entities?
Use the [UserData](xref:CADability.UserData) object and the IGeoObject.[UserData](xref:CADability.GeoObject.IGeoObject.UserData) property. This property acts like a dictionary that lets you add any data to the geometric entity. 
If your data is serializable it will also be saved in the CADability database. If your data implements the IPropertyEntry interface or is a simple data type like int, double or string, it will 
be displayed in the control center (when the object is selected).

## How do I define my own geometric entity? 
You almost never need to define or code your own geometric entities. But still, if you want to, there are several ways to define customized geometric entities:
- Define a class that implements the [IGeoObject](xref:CADability.GeoObject.IGeoObject) and [IJsonSerialize](xref:CADability.IJsonSerialize) interfaces. 
This is certainly a huge task and in most cases not necessary.
- Derive a class from [IGeoObjectImpl](xref:CADability.GeoObject.IGeoObjectImpl). This is a standard implementation of IGeoObject but still leaves a lot of work to do.
- Derive a class from [Block](xref:CADability.GeoObject.Block). Add some additional properties and create child entities according to these properties. 
Maybe override [GetShowProperties](xref:CADability.GeoObject.IGeoObject.GetShowProperties*) to define it's appearance in the control center or property grid.

