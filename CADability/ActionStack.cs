using CADability.Actions;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Threading;
using Action = CADability.Actions.Action;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;
using System.Drawing;
using Point = System.Drawing.Point;

namespace CADability
{
    /// <summary>
    /// Parameter declaration for <see cref="ModelView.DisplayChangedEvent"/>.
    /// </summary>

    public class DisplayChangeArg
    {
        /// <summary>
        /// Reason
        /// </summary>
        public enum Reasons
        {
            /// <summary>
            /// unknown
            /// </summary>
            Unknown,
            /// <summary>
            /// The user is zooming in by using the mouse wheel or the toolbar/menu item.
            /// </summary>
            ZoomIn,
            /// <summary>
            /// The user is zooming out by using the mouse wheel or the toolbar/menu item.
            /// </summary>
            ZoomOut,
            /// <summary>
            /// The user is scrolling up
            /// </summary>
            ScrollUp,
            /// <summary>
            /// The user is scrolling down
            /// </summary>
            ScrollDown,
            /// <summary>
            /// The user is scrolling left
            /// </summary>
            ScrollLeft,
            /// <summary>
            /// The user is scrolling right
            /// </summary>
            ScrollRight
        }
        /// <summary>
        /// Reason why the display is changing
        /// </summary>
        public Reasons Reason;
        /// <summary>
        /// While dragging the scrollbarbutton Dragging is on
        /// </summary>
        public enum DraggingModes
        {
            /// <summary>
            /// The user is not dragging a scrollbar button
            /// </summary>
            NotDragging,
            /// <summary>
            /// The user started to drag a scrollbar button
            /// </summary>
            StartDragging,
            /// <summary>
            /// The user is dragging a scrollbar button
            /// </summary>
            Dragging,
            /// <summary>
            /// The user stopped to drag a scrollbar button
            /// </summary>
            StopDragging
        }
        /// <summary>
        /// See <see cref="DraggingModes"/>
        /// </summary>
        public DraggingModes DraggingMode;
        internal DisplayChangeArg(Reasons why)
        {
            Reason = why;
            DraggingMode = DraggingModes.NotDragging;
        }
    }

    /// <summary>
    /// A stack of actions. The top entry is the active action, the bottom entry usually is the <see cref="SelectObjectsAction"/>.
    /// </summary>
    public class ActionStack
    {
        private System.Collections.Stack Actions;
        private IFrame frame;
        private int liceneseCounter;
        /// <summary>
        /// Creates an empty action stack for the given frame. The frame creates its own action stack, so there is usually no need
        /// to create one.
        /// </summary>
        /// <param name="Frame">The frame in which the actions run</param>
        public ActionStack(IFrame Frame)
        {
            Actions = new System.Collections.Stack(10); // halt mal mit 10 initialisieren, ist absolut unkritisch
            this.frame = Frame;
            liceneseCounter = 0;
        }
        /// <summary>
        /// Starts an action and makes it the active action: calls <see cref="Action.OnSetAction"/>, raises
        /// <see cref="IFrame.ActionStartedEvent"/>, inactivates the previously active action (which may remove itself in
        /// <see cref="Action.OnInactivate"/>), pushes the new action onto the stack and calls its <see cref="Action.OnActivate"/>.
        /// If the action wants to change the tab of the control center (<see cref="Action.ChangeTabInControlCenter"/>), the "Action"
        /// page is brought to front and the previous page is restored when the action is removed. Usually called via
        /// <see cref="IFrame.SetAction"/>.
        /// </summary>
        /// <param name="Action">The action to start</param>
        public void SetAction(Action Action)
        {
            //			if (!Action.WorksOnLayoutView)
            //			{	// das ist jetzt besser gelöst mit "OnlyThisView" etc.
            //				frame.AssureModelView(); // geht nur mit ModelView!
            //			}
            if (Action.ChangeTabInControlCenter)
            {
                Action.returnToTabPage = frame.GetActivePropertyDisplay();
                frame.ShowPropertyDisplay("Action");
            }
            Action.ActionStack = this;
            Action.OnSetAction(); // erstes Ereigniss für die neue Aktion (noch nicht auf dem Stack)
            Frame .RaiseActionStartedEvent(Action);
            Action OldAction = null;
            if (Actions.Count > 0) OldAction = Actions.Peek() as Action;
            // folgende Schleife ist notwendig, da bei Inactivate eine Aktion sich
            // selbst entfernen kann. Dann aber bekommt die drunterliegende ein Activate
            // was gleich wieder von einem Inactivate gefolgt werden muss
            while (OldAction != null)
            {
                OldAction.OnInactivate(Action, false); // alte Aktion inaktivieren (kann sich auch verabschieden)
                if (Actions.Count > 0)
                {
                    if (OldAction != Actions.Peek())
                    {
                        OldAction = Actions.Peek() as Action;
                    }
                    else
                    {
                        OldAction = null;
                    }
                }
                else
                {
                    OldAction = null;
                }
            }
            Actions.Push(Action);
            Action.OnActivate(OldAction, true);
        }
        /// <summary>
        /// The frame this action stack belongs to.
        /// </summary>
        public IFrame Frame { get { return frame; } }
        /// <summary>
        /// Terminates the active action and removes it from the stack: calls its <see cref="Action.OnInactivate"/>, pops it,
        /// activates the action below it (if any) with <see cref="Action.OnActivate"/>, calls <see cref="Action.OnRemoveAction"/>
        /// and raises <see cref="IFrame.ActionTerminatedEvent"/>. When the removed action returns true from
        /// <see cref="Action.AutoRepeat"/>, a new instance of it is created (with its parameterless constructor or a constructor
        /// taking the old action as the only parameter) and started. Actions usually terminate themselves with
        /// <see cref="Action.RemoveThisAction"/>.
        /// </summary>
        /// <exception cref="ActionException">When there is no action on the stack</exception>
        public void RemoveActiveAction()
        {
            Action Action = ActiveAction;
            if (Action != null)
            {
                Action.OnInactivate(null, true);
                Actions.Pop();
                if (Action.ChangeTabInControlCenter)
                {
                    frame.ShowPropertyDisplay(Action.returnToTabPage);
                }
                Action NewActiveAction = ActiveAction;
                if (NewActiveAction != null) NewActiveAction.OnActivate(Action, false);
                Action.OnRemoveAction();
                Frame .RaiseActionTerminatedEvent(Action);
            }
            else
            {
                throw new ActionException("RemoveActiveAction: There is no Action object on the action stack");
            }
            // der Autonmatismus, der eine Aktion automatisch wiederholen lässt.
            if (Action.AutoRepeat())
            {	// die Aktion soll automatisch wiederholt werden.
                System.Reflection.ConstructorInfo ci = Action.GetType().GetConstructor(new Type[] { });
                if (ci != null)
                {
                    object repaction = ci.Invoke(new object[] { });
                    if (repaction != null && repaction is Action)
                    {
                        (repaction as Action).AutoRepeated();
                        this.SetAction(repaction as Action);
                    }
                }
                else
                {	// 2. Versuch: gibt es einen Kontruktor, der genau den gleichen Aktionstyp als einzigen Parameter nimmt
                    ci = Action.GetType().GetConstructor(new Type[] { Action.GetType() });
                    if (ci != null)
                    {
                        try
                        {
                            object repaction = ci.Invoke(new object[] { Action });
                            if (repaction != null && repaction is Action)
                            {
                                (repaction as Action).AutoRepeated();
                                this.SetAction(repaction as Action);
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is ThreadAbortException) throw (e);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// The action on top of the stack, which receives the mouse input, or null when the stack is empty.
        /// </summary>
        public Action ActiveAction
        {
            get
            {
                if (Actions.Count > 0) return Actions.Peek() as Action;
                else return null;
            }
        }
        /// <summary>
        /// Searches the stack from top to bottom for an action of exactly the given type (derived types do not match).
        /// </summary>
        /// <param name="typeOfAction">The type of the action to look for, e.g. <c>typeof(SelectObjectsAction)</c></param>
        /// <returns>The topmost action of that type, or null if there is none</returns>
        public Action FindAction(Type typeOfAction)
        {
            // der Stack in ein Array liefert top of stack als erstes Element
            object[] allAction = Actions.ToArray();
            for (int i = 0; i < allAction.Length; ++i)
            {
                if (allAction[i].GetType() == typeOfAction) return allAction[i] as Action;
            }
            return null;
        }
        /// <summary>
        /// Forwards a mouse down event from a view to the active action, if the action accepts input from that view (actions may
        /// restrict themselves to a certain view, view type or model), and makes that view the action's current mouse view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The mouse event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseDown(MouseEventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.SetCurrentMouseView(View);
                    a.OnMouseDown(e, View);
                }
                ++liceneseCounter; // overflow macht nix, ausprobiert!
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse enter event from a view to the active action, if the action accepts input from that view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseEnter(System.EventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.OnMouseEnter(e, View);
                }
                ++liceneseCounter; // overflow macht nix, ausprobiert!
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse hover event from a view to the active action, if the action accepts input from that view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseHover(System.EventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.OnMouseHover(e, View);
                }
                ++liceneseCounter; // overflow macht nix, ausprobiert!
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse leave event from a view to the active action, if the action accepts input from that view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseLeave(System.EventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.OnMouseLeave(e, View);
                }
                ++liceneseCounter; // overflow macht nix, ausprobiert!
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse move event from a view to the active action, if the action accepts input from that view, and makes that
        /// view the action's current mouse view. When there is no active action or the action does not accept the view, the cursor
        /// of the view is set to "No".
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The mouse event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseMove(MouseEventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.SetCurrentMouseView(View);
                    a.OnMouseMove(e, View);
                }
                else
                {
                    View.SetCursor("No");
                }
                ++liceneseCounter; // overflow macht nix, ausprobiert!
                if (liceneseCounter % 5000 == 10)
                {
                }
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse up event from a view to the active action, if the action accepts input from that view, and makes that
        /// view the action's current mouse view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The mouse event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseUp(MouseEventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.SetCurrentMouseView(View);
                    a.OnMouseUp(e, View);
                }
                {   // folgende Zeile nur zum Aufwecken der open cascade dll
                    // dmit die Prüfung der Lizenz stattfindet
                }
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Forwards a mouse wheel event from a view to the active action, if the action accepts input from that view.
        /// Views call this from their mouse event handlers. Exceptions thrown by the action are caught and ignored.
        /// </summary>
        /// <param name="e">The mouse event arguments</param>
        /// <param name="View">The view in which the event occurred</param>
        public void OnMouseWheel(MouseEventArgs e, IView View)
        {
            try
            {
                Action a = (Actions.Count > 0) ? (Action)Actions.Peek() : null;
                if (a != null && a.AcceptMouseInput(View))
                {
                    a.OnMouseWheel(e, View);
                }
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
        }
        /// <summary>
        /// Asks the active action which tooltip (info text) it wants to display for the given cursor position. Views may use it to
        /// show tooltips while the mouse rests over the view.
        /// </summary>
        /// <param name="ScreenCursorPosition">The cursor position</param>
        /// <param name="View">The view the cursor is in</param>
        /// <returns>An index identifying the info text, to be passed to <see cref="GetInfoProviderText"/>, or -1 if there is no
        /// active action or no info for that position</returns>
        public int GetInfoProviderIndex(Point ScreenCursorPosition, IView View)
        {
            if (Actions.Count > 0) return ((Action)Actions.Peek()).GetInfoProviderIndex(ScreenCursorPosition, View);
            return -1;
        }
        /// <summary>
        /// Returns the tooltip text of the active action for an index returned by <see cref="GetInfoProviderIndex"/>.
        /// </summary>
        /// <param name="Index">The index from <see cref="GetInfoProviderIndex"/></param>
        /// <param name="Level">The requested level of detail</param>
        /// <param name="View">The view the cursor is in</param>
        /// <returns>The text, or null if there is no active action or no text for that index</returns>
        public string GetInfoProviderText(int Index, CADability.UserInterface.InfoLevelMode Level, IView View)
        {
            if (Actions.Count > 0) return ((Action)Actions.Peek()).GetInfoProviderText(Index, Level, View);
            return null;
        }
        /// <summary>
        /// Returns the vertical position at which the active action wants its tooltip for the given index to be displayed.
        /// </summary>
        /// <param name="Index">The index from <see cref="GetInfoProviderIndex"/></param>
        /// <param name="View">The view the cursor is in</param>
        /// <returns>The vertical position in pixels, or 0 if there is no active action</returns>
        public int GetInfoProviderVerticalPosition(int Index, IView View)
        {
            if (Actions.Count > 0) return ((Action)Actions.Peek()).GetInfoProviderVerticalPosition(Index, View);
            return 0;
        }
        /// <summary>
        /// Determines the state (enabled, checked) of a menu command by asking the actions on the stack from top to bottom until one
        /// handles it (see <see cref="ICommandHandler.OnUpdateCommand"/>). Actions lower on the stack are asked too, because e.g.
        /// the <see cref="SelectObjectsAction"/> at the bottom controls many commands while another action runs on top of it.
        /// Called by the frame; exceptions thrown by the actions are caught and ignored.
        /// </summary>
        /// <param name="MenuId">The id of the menu command</param>
        /// <param name="CommandState">The state to fill in</param>
        /// <returns>true if an action determined the state, false otherwise</returns>
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {   // OnUpdateCommand klappert rückwärts durch den Action Stack
            // und ruft die einzelnen OnUpdateCommand auf, bis einer reagiert
            // das ist notwendig, da SelectObjectsAction einiges ein und ausschaltet
            // und sonst während des Zeichnens das Verschieben aktiv ist.
            try
            {
                object[] ar = Actions.ToArray();
                // top of stack wird 0
                for (int i = 0; i < ar.Length; ++i)
                {
                    Action a = ar[i] as Action;
                    if (a != null && a.OnUpdateCommand(MenuId, CommandState)) return true;
                }
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
            return false;
        }
        /// <summary>
        /// Executes a menu command by offering it to the actions on the stack from top to bottom until one handles it
        /// (see <see cref="ICommandHandler.OnCommand"/>). Called by the frame; exceptions thrown by the actions are caught and ignored.
        /// </summary>
        /// <param name="MenuId">The id of the menu command</param>
        /// <returns>true if an action handled the command, false otherwise</returns>
        virtual public bool OnCommand(string MenuId)
        {   // siehe OnUpdateCommand ...
            try
            {
                object[] ar = Actions.ToArray();
                for (int i = 0; i < ar.Length; ++i)
                {
                    Action a = ar[i] as Action;
                    if (a != null && a.OnCommand(MenuId)) return true;
                }
            }
            catch (Exception ex)
            {   // der ActionStack ist der Verteiler der MouseMessages. Falls dort eine Exception auftritt
                // wird die hier gefangen und stört nicht weiter. Das ist eine Forderung von ERSA. Ggf. 
                // hier eine Möglichkeit die Exceeption doch noch weiterzureichen vorsehen.
                if (ex is ThreadAbortException) throw (ex);
            }
            return false;
        }

        /// <summary>
        /// Notifies all actions on the stack, the active action first, that views have been added or removed, by calling
        /// <see cref="Action.OnViewsChanged"/>. Called by the frame when its views change or a project is opened.
        /// </summary>
        public void OnViewsChanged()
        {
            // foreach kann man hier nicht nehmen, denn bei dem Aufruf kann eine Aktion sich abmelden
            // außerdem möchte ich lieber rückwärts gehen, die aktive Aktion zuerst
            // foreach (Action a in Actions) a.OnViewsChanged();
            ArrayList al = new ArrayList(Actions);
            for (int i = al.Count - 1; i >= 0; --i)
            {
                (al[i] as Action).OnViewsChanged();
            }
        }
        private void OnDisplayChanged(object sender, DisplayChangeArg displayChangeArg)
        {
            // foreach (Action a in Actions) a.OnDisplayChanged(displayChangeArg);
            // siehe unter OnViewsChanged
            ArrayList al = new ArrayList(Actions);
            for (int i = al.Count - 1; i >= 0; --i)
            {
                (al[i] as Action).OnDisplayChanged(displayChangeArg);
            }
        }
        internal Action PrevoiusAction(Action action)
        {
            object[] allAction = Actions.ToArray();
            for (int i = 0; i < allAction.Length - 1; ++i)
            {
                if (allAction[i] == action) return allAction[i + 1] as Action;
            }
            return null;
        }
    }
}
