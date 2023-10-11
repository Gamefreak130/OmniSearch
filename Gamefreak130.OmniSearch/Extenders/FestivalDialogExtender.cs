namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class FestivalDialogExtender : ModalExtender<FestivalTicketDialog, IUIFestivalTicketReward>
    {
        protected override IEnumerable<IUIFestivalTicketReward> Materials => HudController.Instance.Model.AllAvailableFestivalTicketRewards;

        public FestivalDialogExtender(FestivalTicketDialog modal) : base(modal)
        {
            int verticalOffset = 30;
            int horizontalOffset = -4;
            WindowBase window = ParentWindow.GetChildByIndex(1);
            window.Area = new(window.Area.TopLeft + new Vector2(0, verticalOffset), window.Area.BottomRight + new Vector2(0, verticalOffset));

            window = ParentWindow.GetChildByIndex(2);
            window.Area = new(window.Area.TopLeft + new Vector2(0, verticalOffset), window.Area.BottomRight + new Vector2(horizontalOffset, 0));

            window = ParentWindow.GetChildByID((uint)FestivalTicketDialog.ControlID.FestivalInventoryTable, true);
            window.Area = new(window.Area.TopLeft + new Vector2(0, verticalOffset), window.Area.BottomRight + new Vector2(0, verticalOffset));

            window = window.GetChildByIndex(1);
            window.Area = new(window.Area.TopLeft, window.Area.BottomRight + new Vector2(-1, 0));

            window = ParentWindow.GetChildByIndex(9);
            window.Area = new(window.Area.TopLeft + new Vector2(0, verticalOffset), window.Area.BottomRight + new Vector2(-390, 0));
        }

        protected override void ClearItems()
        {
            Modal.mRewardInventoryTable.Clear();
            Modal.mRewardInventoryTable.Flush();
        }

        protected override void ProcessResultsTask(IEnumerable<IUIFestivalTicketReward> results)
        {
            foreach (IUIFestivalTicketReward reward in results)
            {
                TableRow tableRow = Modal.mRewardInventoryTable.CreateRow();
                FestivalTicketInventoryRowController festivalTicketInventoryRowController = new(tableRow, Modal.mRewardInventoryTable, reward);
                tableRow.RowController = festivalTicketInventoryRowController;
                Modal.mRewardInventoryTable.AddRow(tableRow);
                festivalTicketInventoryRowController.Disabled = Modal.IsRewardDisabled(reward);
            }
        }

        protected override Document<IUIFestivalTicketReward> SelectDocument(IUIFestivalTicketReward material)
            => new(material.LocName, "", material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(100, 57, 270);

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<IUIFestivalTicketReward>(Corpus));
    }
}
