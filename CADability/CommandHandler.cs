using System;

namespace CADability.UserInterface
{
    /// <summary>
    /// State of a menu entry or toolbar button. Used in the <see cref="ICommandHandler.UpdateCommand"/> event.
    /// </summary>
    public class CommandState
    {
	    /// <summary>
        /// Creates a command state which is enabled, but not checked and no radio
        /// button set.
        /// </summary>
        public CommandState()
        {
            Enabled = true;
            Checked = false;
            Radio = false;
        }
        /// <summary>
        /// Gets or sets the "enabled" state.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the "checked" state.
        /// </summary>
        public bool Checked { get; set; }

        /// <summary>
        /// Gets or sets the "radio" state.
        /// </summary>
        public bool Radio { get; set; }
    }
    /// <summary>
    /// Objects that implement this interface can receive menu command.
    /// Usually used in calls to <see cref="MenuResource.LoadMenuDefinition(string, bool, ICommandHandler)"/> to specify
    /// the target object for that menu.
    /// </summary>

    public interface ICommandHandler
    {
        /// <summary>
        /// Process the command with the given MenuId. Return true if handled, false otherwise.
        /// </summary>
        /// <param name="menuId">Id of the menu command to be processed</param>
        /// <returns>true, if handled, false otherwise</returns>
        bool OnCommand(string menuId);
        /// <summary>
        /// Update the command user interface of the given command. Return true if handled, false otherwise.
        /// </summary>
        /// <param name="menuId">Id of the menu command to be processed</param>
        /// <param name="commandState">State object to modify if appropriate</param>
        /// <returns>true, if handled, false otherwise</returns>
        bool OnUpdateCommand(string menuId, CommandState commandState);
		/// <summary>
		/// Notify that the menu item is being selected. No need to react on this notification
		/// </summary>
		/// <param name="selectedMenu">Id of the menu command that was selected</param>
		/// <param name="selected">true, if selected, false if deselected</param>
		/// <returns></returns>
		void OnSelected(MenuWithHandler selectedMenu, bool selected);
    }

    public class SimpleMenuCommand : ICommandHandler
    {
	    private readonly Func<string, bool> onCommand;
	    private readonly Func<string, CommandState , bool> onUpdateCommand;
	    //private Action<MenuWithHandler> onSelected;
        public static ICommandHandler HandleCommand(Func<string, bool> action)
        {
            return new SimpleMenuCommand(action);
        }
        
        public static ICommandHandler HandleAndUpdateCommand(Func<string, bool> action, Func<string, CommandState, bool> update)
        {
            return new SimpleMenuCommand(action, update);
        }
        public SimpleMenuCommand(Func<string, bool> action, Func<string, CommandState, bool> update)
        {
            onCommand = action;
            onUpdateCommand = update;
        }
        public SimpleMenuCommand(Func<string, bool> action)
        {
            onCommand = action;
        }
        bool ICommandHandler.OnCommand(string menuId)
        {
	        return onCommand!=null && onCommand(menuId);
        }

        void ICommandHandler.OnSelected(MenuWithHandler selectedMenu, bool selected)
        {
	        //onSelected?.Invoke(selectedMenu);
        }

        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
	        return onUpdateCommand == null || onUpdateCommand(menuId, commandState);
        }
    }
}
