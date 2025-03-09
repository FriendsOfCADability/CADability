namespace CADability.UserInterface
{
    /// <summary>
    /// The <see cref="IShowProperty"/> implementation for the models contained in a <see cref="Project"/>.
    /// </summary>

    public class ModelsProperty : IShowPropertyImpl, ICommandHandler
    {
        private Project project;
        public ModelsProperty(Project project)
        {
            this.project = project;
            base.resourceIdInternal = "Models";
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get 
            {
            return MenuResource.LoadMenuDefinition("MenuId.Models", false, this);
            }
        }
        IShowProperty[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IShowProperty[project.GetModelCount()];
                    for (int i = 0; i < project.GetModelCount(); ++i)
                    {
                        subEntries[i] = project.GetModel(i) as IShowProperty;
                    }
                }
                return subEntries;
            }
        }
        public Project Project
        {
            get
            {
                return project;
            }
        }
        public override void Refresh()
        {
            subEntries = null;
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
            }
        }
        #region ICommandHandler Members
        private void AddModel()
        {
            Model m = new Model();
            int ind = 1;
            string name = StringTable.GetFormattedString("Model.Default.Name", ind);
            while (project.FindModel(name) != null)
            {
                ++ind;
                name = StringTable.GetFormattedString("Model.Default.Name", ind);
            }
            m.Name = name;
            project.AddModel(m);
            subEntries = null;
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        private void ImportModel()
        {
            Project pr = Project.ReadFromFile(null); // macht den open dialog
            if (pr != null)
            {
                for (int i = 0; i < pr.GetModelCount(); ++i)
                {
                    Model m = pr.GetModel(i);
                    string name = m.Name;
                    int ind = 1;
                    while (project.FindModel(name) != null)
                    {
                        name = m.Name + ind.ToString();
                        ++ind;
                    }
                    m.Name = name;
                    project.AddModel(m);
                }
            }
            subEntries = null;
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
                propertyTreeView.OpenSubEntries(this, true);
            }
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Models.Add":
                    AddModel();
                    return true;
                case "MenuId.Models.Import":
                    ImportModel();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add ModelsProperty.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }
}
