using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{

    /* Der Grund und die Anwendung dieser Klasse:
	 * Nach dem Deserialisieren wird für alle Objekte, die IDeserializationCallback
	 * implementieren "OnDeserialization" aufgerufen. Ansich genau das was wir brauchen.
	 * Jedoch ist dieser Mechanismus unerträglich langsam, so dass man auf mehrere Stunden
	 * Einlesezeit für große Dateien kommt, die sonst in weniger als einer Minute
	 * eingelesen sind.
	 *	Deshalb folgender Workaround: Die StreamingContext Klasse akzeptiert ein "context"
	 * Objekt, welches frei ist für die Anwendung. Dort hinein kommt ein FinishDeserialization 
	 * Objekt. Jedes Objekt kann in seinem Konstruktor(SerializationInfo info, StreamingContext context)
	 * diesem context Objekt mit der Methode "Add" sich selbst zufügen und somit dafür sorgen,
	 * dass es nach dem Ende des Deserialisierens den Aufruf "DeserializationDone" bekommt. Es
	 * muss dazu natürlich IFinishDeserialization implementieren.
	 * So wird eingelesen:
	 *		FinishDeserialization finishDeserialization = new FinishDeserialization();
	 *		BinaryFormatter formatter = new BinaryFormatter(null,new StreamingContext(StreamingContextStates.File,finishDeserialization));
	 *		... und nach dem Einlesen:
	 *		finishDeserialization.DeserializationDone();
	 * 
	 * und so wird es beim Einlesen eines Objektes, welches den CallBack will, benutzt:
	 *		FinishDeserialization fd = context.Context as FinishDeserialization;
	 *		if (fd!=null) fd.Add(this);
	 * Dieser Mechanismus wird z.Z. dafür verwendet, den Owner eines GeoObjects zu setzen
	 * und die ChangeEvents zu setzen.
	 */



    /// <summary>
    /// [Deprecated], 
    /// Implement this interface to receive the "DeserializationDone" callback after
    /// the object is deserialized. To receive this callback you also hav to add your
    /// object to the appropriate list by doing the following on the Constructor
    /// (SerializationInfo info, StreamingContext context): <code>
    /// FinishDeserialization fd = context.Context as FinishDeserialization;
    /// if (fd!=null) fd.Add(this);
    /// </code>
    /// </summary>

    public interface IFinishDeserialization
    {
        /// <summary>
        /// Will be called after deserialization is done
        /// </summary>
        void DeserializationDone(object[] data);
    }
    /// <summary>
    /// [Deprecated], 
    /// Container for objects that need a callback after deserialization.
    /// These objects must implement <see cref="IFinishDeserialization"/>
    /// </summary>

    public class FinishDeserialization
    {
        /*
		 * Ausbaumöglichkeit:
		 * Hier könnte man noch eine zweite Arraylist gebrauchen, die zusätzliche Daten aufnimmt:
		 * Bei AddToContext gibt man als letzten params Parameter Objekte an, die nur temporär
		 * erzeugt werden. Diese Daten werden bei DeserializationDone() mit im Parameter übergeben.
		 * z.Z. muss sich das Objekt diese Daten selbst merken...
		 */
        private List<IFinishDeserialization> objectsToCall;
        private List<object[]> objectsData; // zusätzliche daten der Objekte
                                            /// <summary>
                                            /// Creates an empty list.
                                            /// </summary>
        public FinishDeserialization()
        {
            objectsToCall = new List<IFinishDeserialization>();
            objectsData = new List<object[]>();
        }
        /// <summary>
        /// Add an object to the list.
        /// </summary>
        /// <param name="toAdd">The object to add</param>
        /// <param name="data">Additional data which is supplied at the call to DeserializationDone</param>
        public void Add(IFinishDeserialization toAdd, params object[] data)
        {
            objectsToCall.Add(toAdd);   // beide Arrays müssen synchron bleiben
            objectsData.Add(data);
        }
        /// <summary>
        /// Each Object that implements IFinishDeserialization calls this in its
        /// Constructor(SerializationInfo info, StreamingContext context), to make sure
        /// its IFinishDeserialization.DeserializationDone after Deserialisation is done.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ToAdd"></param>
        /// <param name="data">additional data which is supplied at the call to DeserializationDone</param>
        public static void AddToContext(StreamingContext context, IFinishDeserialization ToAdd, params object[] data)
        {
            FinishDeserialization fd = context.Context as FinishDeserialization;
            if (fd != null) fd.Add(ToAdd, data);
        }
        /// <summary>
		/// Calls "DeserializationDone" for all the objects in the list.
		/// </summary>
		public void DeserializationDone()
        {
            for (int i = 0; i < objectsToCall.Count; ++i)
            {
                objectsToCall[i].DeserializationDone(objectsData[i]);
            }
        }
    }
}
