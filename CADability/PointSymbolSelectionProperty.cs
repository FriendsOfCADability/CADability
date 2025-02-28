using CADability.GeoObject;
using System.Drawing;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class PointSymbolSelectionProperty : IShowPropertyImpl, ICommandHandler
    {
        private GeoObject.Point point;
        private GeoObject.GeoObjectList multiPoint;
        public PointSymbolSelectionProperty(GeoObject.Point point, string ResourceID)
        {
            this.point = point;
            resourceIdInternal = ResourceID;
        }
        public PointSymbolSelectionProperty(GeoObject.GeoObjectList multiPoint, string ResourceID)
        {   // mehrere Punkte ausgewählt
            this.multiPoint = multiPoint;
            resourceIdInternal = ResourceID;
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.PointSymbol", false, this);
            }
        }

        public override ShowPropertyLabelFlags LabelType
        {
            get { return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu; }
        }

        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            PointSymbol pointSymbol = GetPointSymbol();
            switch (MenuId)
            {
                case "MenuId.PointSymbol.Empty":
                    SetPointSymbol((PointSymbol)((int)pointSymbol & 0x30));
                    return true;
                case "MenuId.PointSymbol.Dot":
                    SetPointSymbol((PointSymbol)(((int)pointSymbol & 0x30) | (int)PointSymbol.Dot));
                    return true;
                case "MenuId.PointSymbol.Plus":
                    SetPointSymbol((PointSymbol)(((int)pointSymbol & 0x30) | (int)PointSymbol.Plus));
                    return true;
                case "MenuId.PointSymbol.Cross":
                    SetPointSymbol((PointSymbol)(((int)pointSymbol & 0x30) | (int)PointSymbol.Cross));
                    return true;
                case "MenuId.PointSymbol.Line":
                    SetPointSymbol((PointSymbol)(((int)pointSymbol & 0x30) | (int)PointSymbol.Line));
                    return true;
                case "MenuId.PointSymbol.Square":
                    SetPointSymbol((PointSymbol)((int)pointSymbol ^ (int)PointSymbol.Square));
                    return true;
                case "MenuId.PointSymbol.Circle":
                    SetPointSymbol((PointSymbol)((int)pointSymbol ^ (int)PointSymbol.Circle));
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            PointSymbol pointSymbol = GetPointSymbol();
            switch (MenuId)
            {
                case "MenuId.PointSymbol.Empty":
                    CommandState.Checked = ((int)pointSymbol & 0x07) == (int)PointSymbol.Empty;
                    return true;
                case "MenuId.PointSymbol.Dot":
                    CommandState.Checked = ((int)pointSymbol & 0x07) == (int)PointSymbol.Dot;
                    return true;
                case "MenuId.PointSymbol.Plus":
                    CommandState.Checked = ((int)pointSymbol & 0x07) == (int)PointSymbol.Plus;
                    return true;
                case "MenuId.PointSymbol.Cross":
                    CommandState.Checked = ((int)pointSymbol & 0x07) == (int)PointSymbol.Cross;
                    return true;
                case "MenuId.PointSymbol.Line":
                    CommandState.Checked = ((int)pointSymbol & 0x07) == (int)PointSymbol.Line;
                    return true;
                case "MenuId.PointSymbol.Square":
                    CommandState.Checked = ((int)pointSymbol & (int)PointSymbol.Square) != 0;
                    return true;
                case "MenuId.PointSymbol.Circle":
                    CommandState.Checked = ((int)pointSymbol & (int)PointSymbol.Circle) != 0;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        private PointSymbol GetPointSymbol()
        {
            PointSymbol pointSymbol = PointSymbol.Empty; // wird hier als Stellvertreter für verschieden verwendet
            if (point != null) pointSymbol = point.Symbol;
            else if (multiPoint != null)
            {
                PointSymbol common = (multiPoint[0] as GeoObject.Point).Symbol;
                for (int i = 1; i < multiPoint.Count; i++)
                {
                    if ((multiPoint[i] as GeoObject.Point).Symbol != common)
                    {
                        common = PointSymbol.Empty;
                        break;
                    }
                }
                pointSymbol = common;
            }
            return pointSymbol;
        }
        private void SetPointSymbol(PointSymbol pointSymbol)
        {
            if (point != null) point.Symbol = pointSymbol;
            else if (multiPoint != null)
            {
                foreach (GeoObject.Point pnt in multiPoint)
                {
                    pnt.Symbol = pointSymbol;
                }
            }
        }
    }
}
