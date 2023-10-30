namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class AdventureShopDialogExtender : ModalExtender<AdventureRewardsShopDialog, IUIAdventureReward>
    {
        // Why is StoreItemList marked with an 'm' when it's a static smh
        protected override IEnumerable<IUIAdventureReward> Materials => AdventureRewardsShopDialog.mStoreItemList;

        public AdventureShopDialogExtender(AdventureRewardsShopDialog modal) : base(modal) 
        {
            int topOffset = 30;
            int bottomOffset = -10;
            WindowBase window = ParentWindow.GetChildByIndex(1);
            window.Area = new(window.Area.TopLeft + new Vector2(0, topOffset), window.Area.BottomRight + new Vector2(0, bottomOffset));

            window = ParentWindow.GetChildByIndex(2);
            window.Area = new(window.Area.TopLeft + new Vector2(0, topOffset), window.Area.BottomRight + new Vector2(0, bottomOffset));

            window = ParentWindow.GetChildByIndex(3);
            window.Area = new(window.Area.TopLeft + new Vector2(0, topOffset), window.Area.BottomRight + new Vector2(0, bottomOffset));

            window = ParentWindow.GetChildByIndex(12);
            window.Area = new(window.Area.TopLeft + new Vector2(0, topOffset), window.Area.BottomRight + new Vector2(0, bottomOffset));

            bottomOffset = -30;
            TableContainer table = ParentWindow.GetChildByID((uint)AdventureRewardsShopDialog.ControlID.TraitInventoryTable, true) as TableContainer;
            table.Area = new(table.Area.TopLeft + new Vector2(0, topOffset), table.Area.BottomRight + new Vector2(0, bottomOffset));
            table.VisibleRows = 6;
        }

        protected override void ClearItems() => Modal.mRewardInventoryTable.Clear();

        protected override void ProcessResultsTask(IEnumerable<IUIAdventureReward> results)
        {
            foreach (IUIAdventureReward reward in results)
            {
                TableRow tableRow = Modal.mRewardInventoryTable.CreateRow();
                AdventureRewardStoreInventoryRowController adventureRewardStoreInventoryRowController = new(tableRow, Modal.mRewardInventoryTable, reward);
                tableRow.RowController = adventureRewardStoreInventoryRowController;
                Modal.mRewardInventoryTable.AddRow(tableRow);
                adventureRewardStoreInventoryRowController.Disabled = Modal.IsRewardDisabled(reward);
            }
        }

        protected override Document<IUIAdventureReward> SelectDocument(IUIAdventureReward material) 
            => new(material.Name, material.Description, material);

        protected override void SetSearchBarLocation() => SearchBar.SetLocation(70, 57, 330);

        protected override ISearchModel<IUIAdventureReward> GetSearchModel() => new ExactMatch<IUIAdventureReward>(Corpus);
    }
}
