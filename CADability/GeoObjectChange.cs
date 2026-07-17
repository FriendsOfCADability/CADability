using System;


/// <summary>
/// Namespace GeoObject
/// </summary>
namespace CADability.GeoObject
{
	/// <summary>
	/// This class is used as a parameter in the <see cref="ChangeDelegate"/> event of IGeoObject
	/// (see <see cref="IGeoObject.WillChangeEvent"/>).
	/// </summary>
	public class GeoObjectChange : ReversibleChange
	{
		/// <summary>
		/// Notifies that only an attribute was changed in contrast to a change of the geometry.
		/// </summary>
		public bool OnlyAttributeChanged;
		/// <summary>
		/// Notifies that this change doesn't require an undo operation
		/// </summary>
		public bool NoUndoNecessary;
		/// <summary>
		/// Creates a new GeoObjectChange object. See the appropriate constructor of <see cref="ReversibleChange"/> for details.
		/// </summary>
		/// <param name="objectToChange">The object which will be or was changed</param>
		/// <param name="interfaceForMethod">the interface on which contains the method or property</param>
		/// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
		/// <param name="parameters">The parameters needed to call this method or property</param>
		public GeoObjectChange(IGeoObject objectToChange, Type interfaceForMethod, string methodOrPropertyName, params object[] parameters)
			: base(objectToChange, interfaceForMethod, methodOrPropertyName, parameters)
		{ }

		/// <summary>
		/// Creates a new GeoObjectChange object. See the appropriate constructor of <see cref="ReversibleChange"/> for details.
		/// </summary>
		/// <param name="objectToChange">The object which will be or was changed</param>
		/// <param name="methodOrPropertyName">the case sensitive name of the method or property</param>
		/// <param name="parameters">The parameters needed to call this method or property</param>
		public GeoObjectChange(IGeoObject objectToChange, string methodOrPropertyName, params object[] parameters)
			: base(objectToChange, methodOrPropertyName, parameters)
		{ }

		public GeoObjectChange(IGeoObject objectToChange, string methodOrPropertyName, object parameter)
			: base(objectToChange, methodOrPropertyName, parameter)
		{ }

		/// <summary>
		/// Creates a new GeoObjectChange object that reflects a modification by the <see cref="ModOp"/> m.
		/// </summary>
		/// <param name="objectToChange">The object which will be or was changed</param>
		/// <param name="m">the ModOp that changes or changed the object</param>
		public GeoObjectChange(IGeoObject objectToChange, ModOp m)
			: base(objectToChange, "ModifyInverse", m)
		{ }

		// Wenn das gebraucht wird, dann mit anderen Parametern versehen sonst droht verwechslung mit obiger Methode (methodOrPropertyName)
		//public GeoObjectChange(IGeoObject objectToChange, string key, object oldValue)
		//    : base(objectToChange.UserData, "Add", key, oldValue)
		//{
		//    OnlyAttributeChanged = true;
		//    NoUndoNecessary = false;
		//}
	}
}
