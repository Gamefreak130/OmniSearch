﻿namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class BlueprintExtender : BuildBuyExtender
    {// TEST resort blueprint mode
        public BlueprintExtender() : base(BlueprintController.sController.GetChildByIndex(1))
        {
            SearchBar.MoveToBack();

            BlueprintController controller = BlueprintController.sController;
            controller.mMiddlePuckWin.VisibilityChange += (_,_) => SetSearchBarVisibility();
            controller.mTabContainer.TabSelect += (_,_) => SetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += SetSearchModel;
            controller.mCatalogGrid.AreaChange += (_, _) => TaskEx.Run(SetSearchBarLocation);
        }

        protected override IEnumerable<Document<object>> Corpus
        {
            get
            {
                BlueprintController controller = BlueprintController.sController;

                List<object> list = UserToolUtils.GetObjectProductListFiltered(0xBFFFFFFF, 0x10000000, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0, controller.mCurrRoomFilter, controller.mRoomCategoryFlagsToExclude, 0);
                controller.mCatalogProductFilter.FilterObjects(list, out _);

                return list.ConvertAll(SelectDocument);
            }
        }

        protected override void ProcessResultsTask(IEnumerable<object> results)
            // TODO Performance
            => BlueprintController.sController.mCatalogGrid.BeginPopulating(BlueprintController.sController.AddGridItem, results, 5, ResourceKey.CreateUILayoutKey("BlueprintCatalogItem", 0), null);

        protected override void ClearItems()
        {
            BlueprintController.sController.mCatalogGrid.AbortPopulating();
            BlueprintController.sController.mCatalogGrid.Clear();
        }

        protected override void SetSearchBarVisibility()
        {
            BlueprintController controller = BlueprintController.sController;
            SearchBar.Visible = controller.mMiddlePuckWin.Visible;
            // TODO refactor
            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
                SetSearchModel();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        private void SetSearchBarLocation()
        {
            BlueprintController controller = BlueprintController.sController;
            controller.ResizeCatalogGrid(controller.mWindowSortByRoom, controller.mGridSortByRoom, controller.mDefaultSortByRoomWidth, controller.mDefaultSortByRoomGridColumns, Math.Max(controller.mGridSortByRoom.Count, 5), true);
            SearchBar.SetLocation(725, -35, BlueprintController.sController.mCatalogGrid.VisibleColumns <= 5 ? 160 : 250);
        }
    }
}