using System;
using System.Collections;

namespace CADability
{
    /// <summary>
    /// Exception class for the <see cref="UndoRedoSystem.UndoFrame"/> class
    /// </summary>

    public class UndoFrameException : ApplicationException
    {
        internal UndoFrameException(string msg) : base(msg)
        {
        }
    }

    /// <summary>
    /// The undo/redo sytem usually exists as a member in the <see cref="Project"/> and is accessed
    /// via <see cref="Project.Undo"/>. The project handles the menu commands "MenuId.Edit.Undo"
    /// and ""MenuId.Edit.Redo" and calls <see cref="UndoLastStep"/> rsp. <see cref="RedoLastStep"/>.
    /// To add a step to the undo system call <see cref="AddUndoStep"/>
    /// </summary>

    public class UndoRedoSystem
    {
        private ArrayList undoSteps;    // enthält entweder ArrayList oder ReversibleChange Objekte
        private ArrayList redoSteps;    // wie undoSteps
        private int maxUndoSteps;
        private Stack undoFrameStack;	// Stack von ArrayList Objekten, leer, wenn kein Frame offen
        private Stack redoFrameStack;   // Stack von ArrayList Objekten, leer, wenn kein Frame offen
                                        /*
                                         * Verhinderung, dass alle einzelnen Mauspositionen einer Mausbewegung, bzw.
                                         * alle Keystrokes in ein bestimmtes Eingabefeld als Undo-Schritt gespeichert werden:
                                         * Bei MouseMove bzw. KeyUp/KeyDown benutzt man "using ContextFrame" als Rahmen um
                                         * anzuzeigen, aus welchem Context die Änderung kommt. Nur die erste Änderung
                                         * eines Kontexts wird in den undoSteps gemerkt. Mit clearContext kann der Kontext
                                         * wieder zurückgesetzt werden, damit ein erneutes Ändern einen erneuten Undo Schritt
                                         * erzeugt
                                         */
        private object inContext; // der Kontext des letzte contextFrames
        private bool ignoreSameContext; // die folgende Änderung kann ignoriert werden
        private bool isUndoing; // true, wenn gerade einUndo läuft
        public bool isRedoing; // true, wenn gerade ein Redo läuft
        private class contextFrame : IDisposable
        {
            private UndoRedoSystem urs;
            public contextFrame(UndoRedoSystem urs, object context)
            {
                this.urs = urs;
                if (urs.inContext != context)
                {
                    if (urs.inContext != null && urs.EndContinousChangesEvent != null) urs.EndContinousChangesEvent(urs);
                    urs.inContext = context;
                    if (urs.inContext != null && urs.BeginContinousChangesEvent != null) urs.BeginContinousChangesEvent(urs);
                }
                else
                {	// das ist nicht der 1. Aufruf mit diesem context, also ignorieren
                    urs.ignoreSameContext = true;
                }
            }
            #region IDisposable Members

            public void Dispose()
            {
                urs.ignoreSameContext = false;
            }

            #endregion
        }
        /// <summary>
        /// After several changes "using contextFrame" you can reset the context so that the following
        /// changes with the same context generate a new undo step.
        /// </summary>
        public void ClearContext()
        {
            if (inContext != null && EndContinousChangesEvent != null) EndContinousChangesEvent(this);
            inContext = null;
            ignoreSameContext = false; // unnötig, zur Sicherheit
        }
        /// <summary>
        /// All calls to <see cref="AddUndoStep"/> that appeare inside the same context are
        /// considered to belong to the same action and override the previous call from the 
        /// same context. So only the last call will be saved in the undo stack. Use ContextFrame with the C# "using" schema
        /// or call IDisposable.Dispose() to close the frame.
        /// </summary>
        /// <param name="context">any kind of object</param>
        /// <returns>the IDisposable interface for the using schema</returns>
		public IDisposable ContextFrame(object context)
        {
            return new contextFrame(this, context);
        }
        private class undoFrame : IDisposable
        {
            private UndoRedoSystem undoRedoSystem;
            private object openUndoFrame;
            public undoFrame(UndoRedoSystem sys)
            {
                if (!sys.isUndoing)
                {
                    undoRedoSystem = sys;
                    openUndoFrame = sys.OpenUndoFrame();
                }
                else
                {
                    undoRedoSystem = null; // keinen undoframe aufmachen währen undo gemacht wird
                }
            }
            #region IDisposable Members

            public void Dispose()
            {
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.CloseUndoFrame(openUndoFrame);
                }
            }

            #endregion
        }
        private class redoFrame : IDisposable
        {
            private UndoRedoSystem undoRedoSystem;
            private object openRedoFrame;
            public redoFrame(UndoRedoSystem sys)
            {
                if (sys.isUndoing)
                {
                    undoRedoSystem = sys;
                    openRedoFrame = sys.OpenRedoFrame();
                }
                else
                {
                    undoRedoSystem = null; // keinen undoframe aufmachen währen undo gemacht wird
                }
            }
            #region IDisposable Members

            public void Dispose()
            {
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.CloseRedoFrame(openRedoFrame);
                }
            }

            #endregion
        }
        private class isInRedo : IDisposable
        {
            private UndoRedoSystem undoRedoSystem;
            public isInRedo(UndoRedoSystem sys)
            {
                undoRedoSystem = sys;
                undoRedoSystem.isRedoing = true;
            }
            #region IDisposable Members

            public void Dispose()
            {
                if (undoRedoSystem != null)
                {
                    undoRedoSystem.isRedoing = false;
                }
            }

            #endregion
        }
#if DEBUG
        internal static int InstanceCounter = 0;
#endif
#if DEBUG
        ~UndoRedoSystem()
        {
            --InstanceCounter;
        }
#endif

        /// <summary>
        /// Only created by the project. 
        /// </summary>
        internal UndoRedoSystem()
        {
#if DEBUG
            ++InstanceCounter;
#endif
            undoSteps = new ArrayList();
            redoSteps = new ArrayList();
            undoFrameStack = new Stack();
            redoFrameStack = new Stack();
            ignoreSameContext = false;
            maxUndoSteps = 100; // muss man explizit setzen, 0 ist einfach zuviel
        }
        internal object Context
        {
            get
            {
                return inContext;
            }
        }
        private void Undo(object ToUndo)
        {   // Rekursives iterieren über die Liste oder einfaches ausführen des Undo
            if (ToUndo is ReversibleChange)
            {
                // System.Diagnostics.Debug.WriteLine("Simple Undo: " + (ToUndo as ReversibleChange).MethodOrPropertyName);
                (ToUndo as ReversibleChange).Undo();
            }
            else
            {   // es muss eine ArrayList sein, also rückwärts iterieren
                ArrayList al = ToUndo as ArrayList;
                // System.Diagnostics.Debug.WriteLine("Array Undo: " + al.Count.ToString());
                for (int i = al.Count - 1; i >= 0; --i)
                {
                    Undo(al[i]);
                }
            }
        }
        /// <summary>
        /// Returns the next undo command if there are any. This will be either a ReversibleChange object
        /// or a untyped ArrayList. Returns null if there are no undosteps available
        /// </summary>
        /// <returns>The next undo command </returns>
        public object PeekLastUndoStep()
        {
            if (undoSteps.Count > 0)
            {
                return undoSteps[undoSteps.Count - 1];
            }
            return null;
        }
        /// <summary>
        /// Returns the next redo command if there are any. This will be either a ReversibleChange object
        /// or a untyped ArrayList. Returns null if there are no redosteps available
        /// </summary>
        /// <returns>The next undo command </returns>
        public object PeekLastRedoStep()
        {
            if (redoSteps.Count > 0)
            {
                return redoSteps[redoSteps.Count - 1];
            }
            return null;
        }
        /// <summary>
        /// Remove all undo and redo actions from the action stack.
        /// </summary>
        public void Clear()
        {
            undoSteps = new ArrayList();
            redoSteps = new ArrayList();
            undoFrameStack = new Stack();
            redoFrameStack = new Stack();
        }
        /// <summary>
        /// Set or get the maximum number of undo steps that will be kept in the undo stack.
        /// When setting this value the undo stack will be cleared.
        /// </summary>
        public int MaxUndoSteps
        {
            get
            {
                return maxUndoSteps;
            }
            set
            {
                maxUndoSteps = value;
                Clear();
            }
        }
        /// <summary>
        /// Executes the undo of the last step. Ususlly called by the project when handling the "MenuId.Edit.Undo" command.
		/// </summary>
		/// <returns>success: true, failure: false</returns>
        public bool UndoLastStep()
        {
            if (undoSteps.Count > 0)
            {
                isUndoing = true;
                try
                {
                    using (new redoFrame(this)) // damit alles zu einem einzigen Redo zusammengefasst wird
                    {
                        Undo(undoSteps[undoSteps.Count - 1]);
                    }
                    undoSteps.RemoveAt(undoSteps.Count - 1);
                }
                finally
                {
                    isUndoing = false;
                }
                return true;
            }
            return false;
        }
        /// <summary>
        /// Executes the redo of the last step. Ususlly called by the project when handling the "MenuId.Edit.Redo" command.
        /// </summary>
        /// <returns>success: true, failure: false</returns>
        public bool RedoLastStep()
        {
            if (redoSteps.Count > 0)
            {
                using (new undoFrame(this)) // damit alles zu einem einzigen Redo zusammengefasst wird
                {
                    using (new isInRedo(this))
                    {
                        Undo(redoSteps[redoSteps.Count - 1]);
                    }
                }
                redoSteps.RemoveAt(redoSteps.Count - 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// The top undostep on the undo stack will be ignored.
        /// </summary>
        /// <returns>success: true, failure: false</returns>
        public bool IgnoreLastStep()
        {
            if (undoSteps.Count > 0)
            {
                undoSteps.RemoveAt(undoSteps.Count - 1);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true, if there is a step on the undo stack 
        /// </summary>
        /// <returns></returns>
        public bool CanUndo()
        {
            return undoSteps.Count > 0;
        }
        /// <summary>
        /// Returns true, if there is a step on the redo stack 
        /// </summary>
        /// <returns></returns>
        public bool CanRedo()
        {
            return redoSteps.Count > 0;
        }
        /// <summary>
        /// Adds an undo step to the undo stack.
        /// </summary>
        /// <param name="step">the step</param>
        public void AddUndoStep(ReversibleChange step)
        {
            if (maxUndoSteps == 0) return;
            if (ignoreSameContext) return;
            if (undoFrameStack.Count > 0)
            {
                if (!isRedoing) redoSteps.Clear(); // Aerotech 27.3.13: eine neue Undofähige Aktion soll den Redostack leer machen
                (undoFrameStack.Peek() as ArrayList).Add(step);
            }
            else if (redoFrameStack.Count > 0)
            {
                (redoFrameStack.Peek() as ArrayList).Add(step);
            }
            else
            {
                if (isUndoing)
                {
                    redoSteps.Add(step);
                }
                else
                {
                    undoSteps.Add(step);
                    if (!isRedoing) redoSteps.Clear(); // Aerotech 27.3.13: eine neue Undofähige Aktion soll den Redostack leer machen
                    while (undoSteps.Count > maxUndoSteps)
                    {
                        undoSteps.RemoveAt(0); // ältesten Eintrag entfernen
                    }
                }
            }
        }
        /// <summary>
        /// Opens an undo frame, which means that all subsequent calls to AddUndoStep
        /// will be considered as a single undo step. Calling UndoLastStep  or selecting
        /// Undo from the Menu will undo all this
        /// steps as a single step. Use the C# "using" construct, since this property
        /// supports the IDisposable interface, which closes the undoframe when Dispose
        /// is called.
        /// </summary>
        public IDisposable UndoFrame
        {
            get
            {
                return new undoFrame(this);
            }
        }
        /// <summary>
        /// Opens an undo frame: all subsequent calls to <see cref="AddUndoStep"/> are considered
        /// as a single undo step until <see cref="CloseUndoFrame"/> is being called. You can use
        /// the property <see cref="UndoFrame"/> instead. UndoFrames may be nested.
        /// </summary>
        /// <returns>an object that will be needed for the corresponding CloseUndoFrame call.</returns>
        public object OpenUndoFrame()
        {
            // System.Diagnostics.Debug.WriteLine("Frame --->");
            undoFrameStack.Push(new ArrayList());
            if (OpeningUndoFrameEvent != null) OpeningUndoFrameEvent(undoFrameStack.Peek());
            return undoFrameStack.Peek();
        }
        internal object OpenRedoFrame()
        {
            // System.Diagnostics.Debug.WriteLine("Frame --->");
            redoFrameStack.Push(new ArrayList());
            return redoFrameStack.Peek();
        }
        /// <summary>
		/// Closes the previously opened undo frame. See <see cref="OpenUndoFrame"/>.
		/// </summary>
		/// <param name="UndoFrame">the result from OpenUndoFrame</param>
        public void CloseUndoFrame(object UndoFrame)
        {
            // System.Diagnostics.Debug.WriteLine("<--- Frame");
            if (UndoFrame != undoFrameStack.Peek()) throw new UndoFrameException("UndoFrame doesn't match last StartUndoFrame result");
            ArrayList al = undoFrameStack.Pop() as ArrayList;
            if (undoFrameStack.Count > 0)
            {
                (undoFrameStack.Peek() as ArrayList).Add(al);
            }
            else
            {
                if (isUndoing)
                {
                    redoSteps.Add(al);
                }
                else
                {
                    if (al.Count > 0)
                    {
                        undoSteps.Add(al);
                        while (undoSteps.Count > maxUndoSteps)
                        {
                            undoSteps.RemoveAt(0); // ältesten Eintrag entfernen
                        }
                    }
                }
            }
            if (ClosingUndoFrameEvent != null) ClosingUndoFrameEvent(UndoFrame);
        }
        internal void CloseRedoFrame(object RedoFrame)
        {
            // System.Diagnostics.Debug.WriteLine("<--- Frame");
            if (RedoFrame != redoFrameStack.Peek()) throw new UndoFrameException("UndoFrame doesn't match last StartUndoFrame result");
            ArrayList al = redoFrameStack.Pop() as ArrayList;
            if (redoFrameStack.Count > 0)
            {
                (redoFrameStack.Peek() as ArrayList).Add(al);
            }
            else
            {
                if (isUndoing)
                {
                    redoSteps.Add(al);
                }
                else
                {
                    undoSteps.Add(al);
                }
            }
        }
        public delegate void BeginContinousChangesDelegate(object source);
        public delegate void EndContinousChangesDelegate(object source);
        public event BeginContinousChangesDelegate BeginContinousChangesEvent;
        public event EndContinousChangesDelegate EndContinousChangesEvent;
        public delegate void OpeningUndoFrameDelegate(object source);
        public delegate void ClosingUndoFrameDelegate(object source);
        public event OpeningUndoFrameDelegate OpeningUndoFrameEvent;
        public event ClosingUndoFrameDelegate ClosingUndoFrameEvent;
    }
}
