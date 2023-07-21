namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class FestivalDialogExtender : ModalExtender<FestivalTicketDialog, IUIFestivalTicketReward>
    {
        protected override IEnumerable<IUIFestivalTicketReward> Materials => HudController.Instance.Model.AllAvailableFestivalTicketRewards;

        public FestivalDialogExtender(FestivalTicketDialog modal) : base(modal)
        {
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
