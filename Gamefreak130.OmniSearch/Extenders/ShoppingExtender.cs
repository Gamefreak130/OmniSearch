namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class ShoppingExtender : DocumentSearchExtender<IShopItem>
    {
        protected override IEnumerable<IShopItem> Materials
            => ShoppingController.Instance.mBuyType switch
            {
                ShoppingController.BuyType.ByItem    => ShoppingController.mStoreItemDictionary[ShoppingController.Instance.mSort.mTag].Cast<IShopItem>(),
                ShoppingController.BuyType.ByRecipe  => ShoppingController.mRecipeList.Cast<IShopItem>(),
                ShoppingController.BuyType.BySell    => ShoppingController.mSellableInventory.Cast<IShopItem>(),
                _                                    => throw new NotSupportedException()
            };

        public ShoppingExtender() : base(ShoppingController.Instance, "Shopping", showFullPanel: false)
        {
            ShoppingController controller = ShoppingController.Instance;
            controller.mInventoryTabContainer.VisibilityChange += (_,_) => SetSearchBarLocation();
            controller.mInventoryTabContainer.TabSelect += OnShoppingListUpdated;
            controller.mByItemButton.Click += OnShoppingListUpdated;
            controller.mByRecipeButton.Click += OnShoppingListUpdated;
            controller.mBySellButton.Click += OnShoppingListUpdated;
        }

        private void OnShoppingListUpdated(object _, object __) => SetSearchModel();

        protected override void ClearItems() => ShoppingController.Instance.mStoreTable.Clear();

        protected override void ProcessResultsTask(IEnumerable<IShopItem> results)
        {
            ShoppingController controller = ShoppingController.Instance;
            TableContainer.TablePopulateCallback callback = controller.mBuyType switch
            {
                ShoppingController.BuyType.ByItem    => controller.CreateShoppingUIRow,
                ShoppingController.BuyType.ByRecipe  => controller.CreateRecipeUIRow,
                ShoppingController.BuyType.BySell    => controller.CreateSellableUIRow,
                _                                    => throw new NotSupportedException()
            };

            if (controller.mBuyType is ShoppingController.BuyType.BySell)
            {
                results = results.GroupBy(item => item.StoreUIItemID + item.ActualPrice)
                                 .Select(grp => {
                                     IShopItem ret = grp.First();
                                     (ret as ISellableUIItem).NumInInventory = grp.Count();
                                     return ret;
                                 });
            }

            controller.mStoreTable.BeginPopulating(callback, results, 5);
        }

        protected override Document<IShopItem> SelectDocument(IShopItem material)
            => material switch
            {
                IBookGeneralUIItem item   => new($"{item.Title}", $"{item.Genre}\t{item.Author}", material),
                IBookUIItem item          => new($"{item.Title}", $"{item.Author}", material),
                IShoppingUIRecipe recipe  => new(recipe.DisplayName, 
                                                 string.Join("\t", recipe.ItemsForRecipe.Select(item => item.DisplayName)
                                                                                               .ToArray()),
                                                 material),
                IShopItem                 => new(material.DisplayName, "", material)
            };

        protected override void SetSearchBarLocation() 
            => SearchBar.SetLocation(60, ShoppingController.Instance.mInventoryTabContainer.Visible ? 141 : 120, 300);

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<IShopItem>(Corpus));
    }
}
