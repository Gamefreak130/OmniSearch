namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class FestivalDialogExtender : ModalExtender<IUIFestivalTicketReward>
    {
        protected override IEnumerable<IUIFestivalTicketReward> Materials => HudController.Instance.Model.AllAvailableFestivalTicketRewards;

        private TableContainer Table => mTable ??= ParentWindow.GetChildByID((uint)FestivalTicketDialog.ControlID.FestivalInventoryTable, true) as TableContainer;

        private TableContainer mTable;

        protected override void ClearItems()
        {
            Table.Clear();
            Table.Flush();
        }

        protected override void ProcessResultsTask(IEnumerable<IUIFestivalTicketReward> results)
        {
            FestivalTicketDialog.mStoreItemList = results.ToList();
            foreach (IUIFestivalTicketReward reward in FestivalTicketDialog.mStoreItemList)
            {
                TableRow tableRow = Table.CreateRow();
                FestivalTicketInventoryRowController festivalTicketInventoryRowController = new(tableRow, Table, reward);
                tableRow.RowController = festivalTicketInventoryRowController;
                Table.AddRow(tableRow);
                festivalTicketInventoryRowController.Disabled = reward.TicketCost > HudController.Instance.Model.CurrentFestivalTickets;
            }
        }

        protected override Document<IUIFestivalTicketReward> SelectDocument(IUIFestivalTicketReward material)
            => new(material.LocName, "", material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(100, 57, 270);

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<IUIFestivalTicketReward>(Corpus));
    }
}
