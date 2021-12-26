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

            mSearchBar = new(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0), OnQueryEntered, 303, -52, 201);

            BuyController.sController.mTabContainerSortByFunction.TabSelect += OnTabSelect;

            uint num = 0U;
            WindowBase childByIndex = BuyController.sController.mCategorySelectionPanel.GetChildByIndex(num);
            while (childByIndex != null)
            {
                Button button2 = childByIndex as Button;
                if (button2 != null)
                {
                    button2.Click += OnCategoryButtonClick;
                }
                childByIndex = BuyController.sController.mCategorySelectionPanel.GetChildByIndex(num += 1U);
            }
        }

        public override void Dispose()
        {
            mSearchBar.Dispose();
            base.Dispose();
        }

        // TODO Fix for null category
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

        private void OnTabSelect(TabControl _, TabControl __)
        {
            try
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
                    TaskEx.Run(() => OnQueryEntered());
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            if (!string.IsNullOrEmpty(mSearchBar.Query))
            {
                BuyController.sController.StopGridPopulation();
                ClearCatalogGrid();
            }
        }
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
