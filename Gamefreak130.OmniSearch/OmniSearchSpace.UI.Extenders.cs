using Gamefreak130.Common.Loggers;
using Gamefreak130.Common.Tasks;
using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI;
using Sims3.UI.Hud;
using Sims3.UI.Store;
using System;
using System.Collections;
using System.Linq;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Search query history using up key
    // CONSIDER Hide/show toggle using tab or something
    // TODO Place search bar behind product flyout
    // TODO Fix shop mode weirdness
    public abstract class SearchExtender : IDisposable
    {
        private ISearchModel mSearchModel;

        protected ISearchModel SearchModel
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

            BuyController.sController.mTabContainerSortByFunction.TabSelect += (_,_) => SetSearchModel();

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
            BuyController.sController.mCategorySelectionPanel.GetChildByID((uint)BuyController.ControlID.LastCategoryTypeButton, true).VisibilityChange += (_,_) => TaskEx.Run(SetSearchBarLocation);

            BuyController.sController.mCollectionGrid.ItemClicked += (_,_) => SetSearchModel();

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

        // TODO Filter by collections if query matches
        protected override void OnQueryEntered()
        {
            string query = mSearchBar.Query.ToLower();
            IEnumerable results = null;
            /*foreach (IBBCollectionData collection in BuyController.sController.mBuyModel.CollectionInfo.CollectionData)
            {
                // CONSIDER Check count and use tfidf search model as fallback?
                if (Regex.Replace(query, SearchModel<object>.kCharsToRemove, "") == Regex.Replace(collection.CollectionName.ToLower(), SearchModel<object>.kCharsToRemove, ""))
                {
                    results = collection.Items.Where(item => (item as BuildBuyPreset).Product);
                }
            }*/
            results ??= SearchModel.Search(query);

            BuyController.sController.StopGridPopulation();
            ClearCatalogGrid();
            BuyController.sController.PopulateGrid(results, BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections ? BuyController.kBuildCatalogPatternItemKey : BuyController.kBuyCatalogItemKey);
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

        private void SetSearchModel()
        {
            try
            {
                if (BuyController.sController.mCurrCatalogType is not BuyController.CatalogType.Inventory)
                {
                    SearchModel = new TFIDF<object>(BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                                                     .Cast<object>()
                                                                                                     .Select(SelectProduct))
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
                else
                {
                    // TODO
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        private Document<object> SelectProduct(object product)
        {
            string name, description;

            switch (product)
            {
                case BuildBuyProduct bbp:
                    name = bbp.CatalogName;
                    description = bbp.Description;
                    break;
                case BuildBuyPreset bbp:
                    name = bbp.Product.CatalogName;
                    description = bbp.Product.Description;
                    // Filter out the generic descriptions of wall or floor patterns
                    if (description == Localization.LocalizeString(0xDD1EAD49D9F75762) || description == Localization.LocalizeString(0x2DE87A7A181E89C4))
                    {
                        description = "";
                    }
                    break;
                case IFeaturedStoreItem fsi:
                    name = fsi.Name;
                    description = fsi.Description;
                    break;
                case IBBCollectionData bbcd:
                    name = bbcd.CollectionName;
                    description = bbcd.CollectionDesc;
                    break;
                default:
                    throw new ArgumentException("Not a valid Build/Buy product", nameof(product));
            }

            return new Document<object>(name, description, product);
        }

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            mSearchBar.Clear();
            SetSearchBarLocation();
            if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections)
            {
                SetSearchModel();
            }
        }

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            mSearchBar.Visible = BuyController.sController.mCatalogGrid.Visible;
            if (!string.IsNullOrEmpty(mSearchBar.Query))
            {
                BuyController.sController.StopGridPopulation();
                ClearCatalogGrid();
            }
        }

        private void OnCatalogGridToggled(WindowBase _, UIVisibilityChangeEventArgs args)
        {
            mSearchBar.Visible = args.Visible;
            mSearchBar.Clear();
            SetSearchModel();
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
