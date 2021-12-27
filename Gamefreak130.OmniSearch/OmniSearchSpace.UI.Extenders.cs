using Gamefreak130.Common.Loggers;
using Gamefreak130.Common.Tasks;
using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using Sims3.Gameplay.EventSystem;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;
using System;
using System.Collections;
using System.Linq;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Search query history using up key
    // CONSIDER Hide/show toggle using tab or something
    public abstract class SearchExtender : IDisposable
    {
        private ISearchModel<BuildBuyProduct> mSearchModel;

        protected ISearchModel<BuildBuyProduct> SearchModel
        {
            get => mSearchModel;
            set
            {
                mSearchModel?.Dispose();
                mSearchModel = value;
            }
        }

        protected SearchExtender()
            => EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                    Dispose();
                    return ListenerAction.Remove;
                });

        public virtual void Dispose() => SearchModel = null;

        protected abstract void OnQueryEntered();
    }

    public class BuyExtender : SearchExtender
    {
        protected readonly OmniSearchBar mSearchBar;

        public BuyExtender()
        {
            mSearchBar = new(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0), OnQueryEntered);
            SetSearchBarLocation();
            TaskEx.Run(SetSearchModel);

            BuyController.sController.mTabContainerSortByFunction.TabSelect += OnTabSelect;
            BuyController.sController.mTabContainerSortByRoom.TabSelect += (_,_) => TaskEx.Run(SetSearchModel);

            foreach (BuyController.RoomCatalogButtonInfo roomInfo in BuyController.mRoomCatalogButtonInfoDict.Values)
            {
                if (BuyController.sController.GetChildByID(roomInfo.mButtonId, true) is Button button)
                {
                    button.Click += OnRoomSelect;
                }
            }

            uint num = 0;
            WindowBase childByIndex = BuyController.sController.mCategorySelectionPanel.GetChildByIndex(num);
            while (childByIndex is not null)
            {
                if (childByIndex is Button button)
                {
                    button.Click += OnCategoryButtonClick;
                }
                childByIndex = BuyController.sController.mCategorySelectionPanel.GetChildByIndex(num += 1);
            }

            BuyController.sController.mCatalogCategoryModeButton.Click += OnCatalogButtonClick;
            BuyController.sController.mCatalogRoomModeButton.Click += OnCatalogButtonClick;
            BuyController.sController.mCatalogInventoryModeButton.Click += OnCatalogButtonClick;
            BuyController.sController.mCollectionButton.Click += OnCatalogButtonClick;
            BuyController.sController.mButtonShopModeSortByRoom.Click += OnCatalogButtonClick;
            BuyController.sController.mButtonShopModeSortByFunction.Click += OnCatalogButtonClick;
            BuyController.sController.mButtonShopModeNotable.Click += OnCatalogButtonClick;
            BuyController.sController.mButtonShopModeEmpty.Click += OnCatalogButtonClick;
            BuyController.sController.mCategorySelectionPanel.GetChildByID((uint)BuyController.ControlID.LastCategoryTypeButton, true).VisibilityChange += (_,_) => SetSearchBarLocation();

            // TODO Force by room grid to always be at least three columns
            //BuyController.sController.mGridSortByRoom.AreaChange += OnByRoomGridResize;
            BuyController.sController.mGridSortByRoom.Grid.VisibilityChange += OnCatalogGridToggled;
            BuyController.sController.mCollectionCatalogWindow.VisibilityChange += OnCatalogGridToggled;
            BuyController.sController.mGridSortByFunction.Grid.VisibilityChange += OnCatalogGridToggled;
        }


        public override void Dispose()
        {
            mSearchBar.Dispose();
            base.Dispose();
        }

        // TODO Fix for null category
        // TODO Filter by collections if query matches
        protected override void OnQueryEntered()
        {
            BuyController.sController.StopGridPopulation();
            IEnumerable results = SearchModel.Search(mSearchBar.Query);
            ClearCatalogGrid();
            BuyController.sController.PopulateGrid(results, BuyController.kBuyCatalogItemKey);
        }

        private void ClearCatalogGrid()
        {
            foreach (ItemGridCellItem itemGridCellItem in BuyController.sController.mCatalogGrid.Items)
            {
                if (BuyController.sController.mCurrCatalogType is not BuyController.CatalogType.Collections)
                {
                    BuyController.sController.mBuyCatalogGridItemCache.Add(itemGridCellItem.mWin);
                }
                else
                {
                    BuyController.sController.mBuildCatalogPatternGridItemCache.Add(itemGridCellItem.mWin);
                }
            }
            BuyController.sController.mCatalogGrid.Clear();
            BuyController.sController.mNumObjectsInGrid = 0;
            BuyController.sController.ResizeCatalogGrid(BuyController.sController.mCatalogGrid, SearchModel.DocumentCount);
        }

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __) => SetSearchBarLocation();

        private void SetSearchBarLocation()
        {
            BuyController.CatalogType catalogType = BuyController.sController.mCurrCatalogType;
            float x, y = -35, width;

            if (catalogType == BuyController.CatalogType.ByRoom)
            {
                x = 720;
                width = 224;
            }
            else
            {
                width = catalogType == BuyController.CatalogType.ByCategory && BuyController.sBuyDebug ? 201 : 251;
                if (BuyController.sController.mBuyModel.IsInShopMode())
                {
                    x = 362;
                    width -= 45;
                }
                else
                {
                    x = 317;
                }

                if (catalogType is BuyController.CatalogType.Inventory)
                {
                    mSearchBar.Visible = true;
                }
                if (catalogType is BuyController.CatalogType.Collections)
                {
                    mSearchBar.Visible = BuyController.sController.mCollectionCatalogWindow.Visible;
                }
            }
            mSearchBar.SetLocation(x, y, width);
        }

        private void OnTabSelect(TabControl _, TabControl __) => SetSearchModel();

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private void SetSearchModel()
        {
            try
            {
                if (BuyController.sController.mPopulateGridTaskHelper.Collection.OfType<object>().First() is BuildBuyProduct)
                {
                    SearchModel = new TFIDF<BuildBuyProduct>(BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                                                              .Cast<BuildBuyProduct>()
                                                                                                              .Select(product => new Document<BuildBuyProduct>(product.CatalogName, product.Description, product)))
                    {
                        Yielding = true
                    };

                    SearchModel.Preprocess();

                    if (!string.IsNullOrEmpty(mSearchBar.Query))
                    {
                        BuyController.sController.StopGridPopulation();
                        ClearCatalogGrid();
                        TaskEx.Run(OnQueryEntered);
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            mSearchBar.Visible = BuyController.sController.mCatalogGrid.Visible;
            if (!string.IsNullOrEmpty(mSearchBar.Query))
            {
                BuyController.sController.StopGridPopulation();
                ClearCatalogGrid();
            }
        }

        private void OnCatalogGridToggled(WindowBase _, UIVisibilityChangeEventArgs args) => mSearchBar.Visible = args.Visible;
    }

    /*public static class BuildExtender
    {
        private static bool sDone;

        public static void Init()
        {
            BuildController.sController.Tick += TriggerSearch;
        }

        private static void TriggerSearch(WindowBase _, UIEventArgs __)
        {
            try
            {
                if (!(BuyController.sController.mCatalogGrid.Tag as Button).Enabled)
                {
                    sDone = false;
                    return;
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }
    }*/
}
