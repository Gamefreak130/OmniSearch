namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class BlueprintExtender : BuildBuyExtender
    {// TEST resort blueprint mode
        protected override IEnumerable<object> Materials
        {
            get
            {
                BlueprintController controller = BlueprintController.sController;

                List<object> list = UserToolUtils.GetObjectProductListFiltered(0xBFFFFFFF, 0x10000000, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0, controller.mCurrRoomFilter, controller.mRoomCategoryFlagsToExclude, 0);
                controller.mCatalogProductFilter.FilterObjects(list, out _);
                return list;
            }
        }

        public BlueprintExtender() : base(BlueprintController.sController.GetChildByIndex(1))
        {
            SearchBar.MoveToBack();

            BlueprintController controller = BlueprintController.sController;
            controller.mMiddlePuckWin.VisibilityChange += (_,_) => RefreshSearchBarVisibility();
            controller.mTabContainer.TabSelect += (_,_) => SetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += SetSearchModel;
            controller.mCatalogGrid.AreaChange += (_, _) => TaskEx.Run(SetSearchBarLocation);
            RefreshSearchBarVisibility();
        }

        protected override void ProcessResultsTask(IEnumerable<object> results)
            => BlueprintController.sController.mCatalogGrid.BeginPopulating(BlueprintController.sController.AddGridItem, results, 1, ResourceKey.CreateUILayoutKey("BlueprintCatalogItem", 0), null);

        protected override void ClearItems()
        {
            BlueprintController.sController.mCatalogGrid.AbortPopulating();
            BlueprintController.sController.mCatalogGrid.Clear();
        }

        protected override void RefreshSearchBarVisibility()
        {
            if (BlueprintController.sController is not null)
            {
                SetSearchBarVisibility(BlueprintController.sController.mMiddlePuckWin.Visible);
            }
        }

        protected override void SetSearchBarLocation()
        {
            BlueprintController controller = BlueprintController.sController;
            controller.ResizeCatalogGrid(controller.mWindowSortByRoom, controller.mGridSortByRoom, controller.mDefaultSortByRoomWidth, controller.mDefaultSortByRoomGridColumns, Math.Max(controller.mGridSortByRoom.Count, 5), true);
            SearchBar.SetLocation(725, -35, BlueprintController.sController.mCatalogGrid.VisibleColumns <= 5 ? 160 : 250);
        }
    }
}
