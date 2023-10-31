namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER BuildCatalogItem vs. BuildPatternItem in search results
    public class BuildExtender : BuildBuyExtender
    {
        private bool mCompositorActive;

        private bool mSellPanelActive;

        protected override IEnumerable<object> Materials => BuildController.sController.mProductList ?? BuildController.sController.mPresetList;

        protected override bool IsSearchBarVisible 
            => BuildController.sController.mMiddlePuckWin.Visible && BuildController.sController.mCurrentCatalogGrid is not null 
            && (!BuildController.sCollectionMode || BuildController.sController.mCollectionCatalogWindow.Visible);

        public BuildExtender() : base(BuildController.sLayout.GetWindowByExportID(1).GetChildByIndex(2))
        {
            SearchBar.MoveToBack();

            World.OnEyedropperPickCallback += OnEyedropperPick;

            BuildController controller = BuildController.sController;

            controller.mPoolSelectObjectsButton.Click += OnCategorySelected;
            controller.mPoolObjectsSelectPoolButton.Click += OnCategorySelected;

            controller.mFountainSelectObjects.Click += OnCategorySelected;
            controller.mFountainObjectsSelectFountainButton.Click += OnCategorySelected;

            controller.mWallPaintSelectWallButton.Click += OnCategorySelected;
            controller.mWallCreateSelectPaintButton.Click += OnCategorySelected;
            controller.mWallCreateHalfWallButton.Click += OnCategorySelected;
            controller.mHalfWallCreateSelectPaintButton.Click += OnCategorySelected;
            controller.mHalfWallCreateWallButton.Click += OnCategorySelected;
            controller.mHalfWallCreateRoomButton.Click += OnCategorySelected;
            controller.mHalfWallCreateDiagonalRoomButton.Click += OnCategorySelected;

            controller.mFenceCreateGateButton.Click += OnCategorySelected;
            controller.mGatesCreateFenceButton.Click += OnCategorySelected;

            controller.mStairsCatalogRailingsButton.Click += OnCategorySelected;
            controller.mStairsCatalogElevatorsButton.Click += OnCategorySelected;
            controller.mRailingsCatalogStairsButton.Click += OnCategorySelected;
            controller.mRailingsCatalogElevatorsButton.Click += OnCategorySelected;
            controller.mElevatorsCatalogStairsButton.Click += OnCategorySelected;
            controller.mElevatorsCatalogRailingsButton.Click += OnCategorySelected;

            controller.mFoundationCatalogSquareButton.Click += OnCategorySelected;
            controller.mFoundationCatalogDiagonalButton.Click += OnCategorySelected;

            (controller.GetChildByID((uint)BuildController.ControlID.TerrainPaintButton, true) as Button).Click += OnCategorySelected;
            (controller.GetChildByID((uint)BuildController.ControlID.TerrainEraseButton, true) as Button).Click += OnCategorySelected;

            controller.mCollectionGrid.ItemClicked += (_, _) => ResetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += ResetSearchModel;

            controller.mConstructionCatalogTabContainer.TabSelect += OnTabSelect;
            controller.mFloorPaintTabContainer.TabSelect += OnTabSelect;
            controller.mGardeningCatalogTabContainer.TabSelect += OnTabSelect;
            controller.mGenericCatalogTabContainer.TabSelect += OnTabSelect;
            controller.mRoofCatalogTabContainer.TabSelect += OnTabSelect;
            controller.mStairsTabContainer.TabSelect += OnTabSelect;
            controller.mTerrainPaintTabContainer.TabSelect += OnTabSelect;
            controller.mWallPaintTabContainer.TabSelect += OnTabSelect;

            controller.mMiddlePuckWin.VisibilityChange += OnMiddlePuckVisibilityChange;
            controller.mCollectionCatalogWindow.VisibilityChange += OnMiddlePuckVisibilityChange;
        }

        public override void Dispose()
        {
            World.OnEyedropperPickCallback -= OnEyedropperPick;
            base.Dispose();
        }

        protected override void ClearItems()
        {
            BuildController.sController.mCurrentCatalogGrid.AbortPopulating();
            BuildController.sController.mCurrentCatalogGrid.Clear();
        }

        protected override void RefreshSearchBar()
        {
            BuildController controller = BuildController.sController;
            SetSearchBarVisibility();

            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
                if (mCompositorActive || mSellPanelActive)
                {
                    if (mCompositorActive)
                    {
                        ProcessExistingQuery();
                    }
                    mCompositorActive = false;
                    mSellPanelActive = false;
                }
                else
                {
                    ResetSearchModel();
                    // In certain cases the catalog grid doesn't actually repopulate if you search, go back, then select the same tool again
                    // Even though the search bar itself is cleared
                    // This ensures that the previous filtered results don't get "stuck" when this happens
                    if (string.IsNullOrEmpty(SearchBar.Query))
                    {
                        controller.mCatalogNeedsARefresh = true;
                        controller.RebuildCatalog();
                    }
                }
            }
            // These are set to false before the middle puck reappears
            // So we need to keep track of them ourselves to avoid needlessly resetting the search model
            else if (controller.mbCompositorActive)
            {
                mCompositorActive = true;
            }
            else if (controller.mSellPanelController.Visible)
            {
                mSellPanelActive = true;
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected override void SetSearchBarLocation()
        {
            BuildController controller = BuildController.sController;
            float x,
                  y = controller.mCollectionWindow.Visible ? -36 : -37,
                  width;

            switch (controller)
            {
                case { mPoolObjectsCatalogWindow.Visible: true } or { mGatesCatalogWindow.Visible: true } or { mRailingsCatalogWindow.Visible: true } or { mElevatorsCatalogWindow.Visible: true }:
                    x = 75;
                    width = 250;
                    break;
                case { mFoundationCatalogWindow.Visible: true }:
                    x = 93;
                    width = 260;
                    break;
                case { mTerrainPaintTypesWindow.Visible: true }:
                    x = 85;
                    width = 268;
                    break;
                case { mFenceCatalogWindow.Visible: true }:
                    x = 140;
                    width = 250;
                    break;
                case { mHalfWallCatalogWindow.Visible: true }:
                    x = 190;
                    width = 250;
                    break;
                case { mWallCreateAndPaintWindow.Visible: true }:
                    x = 535;
                    width = MathUtils.Clamp((65 * controller.mCurrentCatalogGrid.VisibleColumns) - 435, 150, 250);
                    break;
                case { mFloorPaintTypesWindow.Visible: true }:
                    x = 518;
                    width = MathUtils.Clamp((65 * controller.mCurrentCatalogGrid.VisibleColumns) - 465, 120, 250);
                    break;
                case { mConstructionCatalogWindow.Visible: true }:
                    x = 270;
                    width = 300;
                    break;
                case { mGenericCatalogWindow.Visible: true }:
                    x = 180;
                    width = 300;
                    break;
                case { mGardeningCatalogWindow.Visible: true }:
                    x = 220;
                    width = 300;
                    break;
                default:
                    x = 350;
                    width = 260;
                    break;
            }

            SearchBar.SetLocation(x, y, width);
        }

        protected override ISearchModel<object> GetSearchModel()
            => BuildController.sController.mFloorPaintTypesWindow.Visible || BuildController.sController.mWallCreateAndPaintWindow.Visible
            ? new ExactMatch<object>(Corpus)
            : base.GetSearchModel();

        private void OnCategorySelected(object _, EventArgs __)
        {
            SearchBar.Clear();
            RefreshSearchBar();
        }

        private void OnTabSelect(TabControl _, TabControl __) => ResetSearchModel();

        private void OnMiddlePuckVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __) => RefreshSearchBar();

        private void OnEyedropperPick(object _, EventArgs __)
        {
            OnCategorySelected(_, __);
            ClearItems();
            SearchBar.TriggerSearch();
        }

        protected override void ProcessResultsTask(IEnumerable<object> results)
        {
            BuildController controller = BuildController.sController;

            List<object> productList = controller.mProductList is not null ? results.ToList() : null,
                         presetList = controller.mPresetList is not null ? results.ToList() : null;

            controller.PopulateCatalogGrid(controller.mCurrentCatalogGrid, "BuildCatalogItem", productList, presetList, controller.mCatalogProduct, controller.mCatalogFilter);
        }
    }
}
