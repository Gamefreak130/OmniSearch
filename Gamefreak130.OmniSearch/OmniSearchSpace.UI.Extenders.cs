using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI.Hud;
using Sims3.UI.Store;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Search query history using up key
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO Fix shop mode weirdness
    // TODO Fix Eyedropper build -> buy null reference exception on query task
    // TODO Hide search bar on object move/CAS
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

        protected abstract void QueryEnteredTask();
    }

    public class BuyExtender : SearchExtender
    {
        protected readonly OmniSearchBar mSearchBar;

        protected readonly IInventory mFamilyInventory;

        protected const ulong kWallDescriptionHash = 0xDD1EAD49D9F75762;

        protected const ulong kFloorDescriptionHash = 0x2DE87A7A181E89C4;

        protected const uint kPreviewPanelId = 0x6E23360;

        private bool mInventoryEventRegistered;

        private IEnumerable<Document<object>> mDocuments;

        private AwaitableTask mPendingQueryTask;

        public BuyExtender()
        {
            mSearchBar = new(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0), QueryEnteredTask);
            mSearchBar.MoveToBack();
            SetSearchBarLocation();

            mFamilyInventory = BuyController.sController.mFamilyInventory;
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged += SetSearchModel;
            }

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
            BuyController.sController.mCatalogProductFilter.FiltersChanged += SetSearchModel;

            BuyController.sController.mGridSortByRoom.AreaChange += (_,_) => TaskEx.Run(SetSearchBarLocation);
            BuyController.sController.mGridSortByRoom.Grid.VisibilityChange += OnCatalogGridToggled;
            BuyController.sController.mCollectionCatalogWindow.VisibilityChange += OnCatalogGridToggled;
            BuyController.sController.mGridSortByFunction.Grid.VisibilityChange += OnCatalogGridToggled;
        }

        public override void Dispose()
        {
            mSearchBar.Dispose();
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged -= SetSearchModel;
            }
            mPendingQueryTask?.Dispose();
            base.Dispose();
        }

        protected override void QueryEnteredTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));
                IEnumerable results = null;
                if (BuyController.sController.mCurrCatalogType is not (BuyController.CatalogType.Collections or BuyController.CatalogType.Inventory))
                {
                    ITokenizer tokenizer = Tokenizer.Create();
                    foreach (IBBCollectionData collection in BuyController.sController.mBuyModel.CollectionInfo.CollectionData)
                    {
                        if (tokenizer.Tokenize(mSearchBar.Query).SequenceEqual(tokenizer.Tokenize(collection.CollectionName)))
                        {
                            List<BuildBuyProduct> collectionProducts = collection.Items.ConvertAll(item => (item as BuildBuyPreset).Product);
                            IEnumerable<BuildBuyProduct> collectionDocs = from document in mDocuments
                                                                          select document.Tag as BuildBuyProduct into product
                                                                          where collectionProducts.Contains(product)
                                                                          select product;

                            if (collectionDocs.FirstOrDefault() is not null)
                            {
                                results = collectionDocs;
                            }
                        }
                    }
                }
                results ??= SearchModel.Search(mSearchBar.Query);

                ClearCatalogGrid();
                if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory)
                {
                    PopulateInventory(results);
                }
                else
                {
                    BuyController.sController.PopulateGrid(results, BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections ? BuyController.kBuildCatalogPatternItemKey : BuyController.kBuyCatalogItemKey);
                }
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
            }
        }

        private void PopulateInventory(IEnumerable results)
        {
            int num = BuyController.sController.mCatalogGrid.GetFirstVisibleItem();

            foreach (IInventoryItemStack stack in results)
            {
                InventoryItemWin inventoryItemWin = InventoryItemWin.MakeEmptySlot();
                inventoryItemWin.Thumbnail = stack.TopObject;
                inventoryItemWin.StackItemCount = stack.Count;
                inventoryItemWin.InUse = stack.InUse;
                if (inventoryItemWin.StackItemCount > 1)
                {
                    inventoryItemWin.GrabAllHandleWin.MouseDown += BuyController.sController.OnGrabAllHandleWinMouseDown;
                    inventoryItemWin.GrabAllHandleWin.MouseUp += BuyController.sController.OnGrabAllHandleWinMouseUp;
                    inventoryItemWin.GrabAllHandleWin.DragEnter += BuyController.sController.OnGridDragEnter;
                    inventoryItemWin.GrabAllHandleWin.DragLeave += BuyController.sController.OnGridDragLeave;
                    inventoryItemWin.GrabAllHandleWin.DragDrop += OnGridDragDrop;
                    inventoryItemWin.GrabAllHandleWin.DragOver += BuyController.sController.OnGridDragOver;
                    inventoryItemWin.GrabAllHandleWin.FocusAcquired += BuyController.sController.OnGrabAllHandleFocusAcquired;
                    inventoryItemWin.GrabAllHandleWin.FocusLost += BuyController.sController.OnGrabAllHandleFocusLost;
                }
                inventoryItemWin.Tag = stack;
                BuyController.sController.mCatalogGrid.AddItem(new(inventoryItemWin, stack));
            }

            if (mFamilyInventory.Count > 0)
            {
                BuyController.sController.mFamilyInventoryInstructionText.Visible = false;
                BuyController.sController.mFamilyInventorySellAllButton.Enabled = mFamilyInventory.Count == BuyController.sController.mCatalogGrid.Count;
            }
            else
            {
                BuyController.sController.mFamilyInventoryInstructionText.Visible = true;
                BuyController.sController.mFamilyInventorySellAllButton.Enabled = false;
            }
            if (num >= 0)
            {
                num += BuyController.sController.mCatalogGrid.InternalGrid.ColumnCount;
                if (num > BuyController.sController.mCatalogGrid.Count)
                {
                    num = BuyController.sController.mCatalogGrid.Count - 1;
                }
                BuyController.sController.mCatalogGrid.MakeItemVisible(num);
            }
            BuyController.sController.ResizeCatalogGrid(BuyController.sController.mWindowFamilyInventory, BuyController.sController.mGridInventory, BuyController.sController.mDefaultInventoryWidth, BuyController.sController.mDefaultInventoryGridColumns, 0x7FFFFFFE, false);
        }

        private void OnGridDragDrop(WindowBase sender, UIDragEventArgs eventArgs)
        {
            try
            {
                if (eventArgs.Data is LiveDragData)
                {
                    BuyController.sController.mbDragging = false;
                    int num = BuyController.sController.mCatalogGrid.GetItemIndexAtPosition(BuyController.sController.mCatalogGrid.ScreenToWindow(sender.WindowToScreen(eventArgs.MousePosition)));
                    if (num >= 0 && !string.IsNullOrEmpty(mSearchBar.Query))
                    {
                        num = mFamilyInventory.InventoryItems.IndexOf(BuyController.sController.mCatalogGrid.Items[num].mTag as IInventoryItemStack);
                    }
                    if (BuyController.sController.mInsertionType is InventoryItemWin.InsertionType.After)
                    {
                        num++;
                    }
                    bool flag = BuyController.sController.mInsertionType is not InventoryItemWin.InsertionType.None;
                    bool result;
                    if (BuyController.sController.mDragInfo?.mDragOriginatingInventory is IInventory originatingInventory)
                    {
                        BuyController.sController.mDragInfo.mDragDestinationInventory = mFamilyInventory;
                        if (originatingInventory == BuyController.sController.mDragInfo.mDragDestinationInventory && (BuyController.sController.mDragInfo.mbDragOriginIsStack || BuyController.sController.mDragInfo.mDragOriginStack.Count == 1))
                        {
                            int num2 = originatingInventory.InventoryItems.IndexOf(BuyController.sController.mDragInfo.mDragOriginStack);
                            if (num2 == num && BuyController.sController.mInsertionType is InventoryItemWin.InsertionType.Stack)
                            {
                                BuyController.sController.mInsertionType = InventoryItemWin.InsertionType.Before;
                            }
                        }
                        BuyController.sController.mDragInfo.mDragDestinationIndex = num;
                        BuyController.sController.mDragInfo.mbDragDestinationInsert = flag;
                        result = true;
                    }
                    else
                    {
                        result = true;
                        mFamilyInventory.InventoryChanged -= BuyController.sController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= SetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= BuyController.sController.HandToolPickupHandler;
                        if (flag)
                        {
                            result = BuyController.sController.AddDraggedObjectsToFamilyInventory(num);
                            if (result)
                            {
                                BuyController.sController.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        else if (BuyController.sController.mLiveDragHelper.DragIsFromInventoryToInventory())
                        {
                            eventArgs.Result = BuyController.sController.mLiveDragHelper.TryAddHandToolObjectsToInventory(mFamilyInventory);
                        }
                        else
                        {
                            result = BuyController.sController.AddDraggedObjectsToFamilyInventory(-1);
                            if (result)
                            {
                                BuyController.sController.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        mFamilyInventory.InventoryChanged += BuyController.sController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += SetSearchModel;
                        World.OnHandToolRotateToMoveCallback += BuyController.sController.HandToolPickupHandler;
                        SetSearchModel();
                        if (string.IsNullOrEmpty(mSearchBar.Query))
                        {
                            BuyController.sController.RepopulateInventory();
                        }
                    }
                    eventArgs.Result = result;
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        private void OnGridDragEnd(WindowBase sender, UIDragEventArgs eventArgs)
        {
            try
            {
                if (eventArgs.Data is LiveDragData)
                {
                    BuyController.sController.mbDragging = false;
                    int num = -1;
                    InventoryItemWin originWin = BuyController.sController.mDragInfo?.mDragOriginWin;
                    BuyController.sController.mFamilyInventorySellAllButton.Visible = true;
                    BuyController.sController.mFamilyInventorySellButton.Visible = false;
                    if (eventArgs.Result)
                    {
                        bool flag = true;
                        mFamilyInventory.InventoryChanged -= BuyController.sController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= SetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= BuyController.sController.HandToolPickupHandler;
                        if (BuyController.sController.mDragInfo is not null)
                        {
                            IInventory destinationInventory = BuyController.sController.mDragInfo.mDragDestinationInventory;
                            if (BuyController.sController.mDragInfo.mDragOriginStack is IInventoryItemStack originStack && BuyController.sController.mDragInfo.mDragOriginatingInventory is IInventory originatingInventory)
                            {
                                int num2 = originatingInventory.InventoryItems.IndexOf(originStack);
                                int count = originatingInventory.InventoryItems.Count;
                                if (BuyController.sController.mDragInfo.mbDragOriginIsStack && destinationInventory is not null)
                                {
                                    flag &= originatingInventory.TryRemoveStackFromInventory(originStack);
                                }
                                else
                                {
                                    flag &= originatingInventory.TryRemoveTopObjectFromStack(originStack);
                                }
                                if (flag && destinationInventory == originatingInventory && BuyController.sController.mDragInfo.mbDragDestinationInsert && num2 < BuyController.sController.mDragInfo.mDragDestinationIndex)
                                {
                                    BuyController.sController.mDragInfo.mDragDestinationIndex -= count - originatingInventory.InventoryItems.Count;
                                }
                            }
                            if (destinationInventory is not null)
                            {
                                if (BuyController.sController.mDragInfo.mbDragDestinationInsert)
                                {
                                    flag &= destinationInventory.TryInsertNewStackAt(BuyController.sController.mLiveDragHelper.DraggedObjects, BuyController.sController.mDragInfo.mDragDestinationIndex, BuyController.sController.mInsertionType == InventoryItemWin.InsertionType.Stack);
                                    BuyController.sController.mLiveDragHelper.PurgeHandToolObjects();
                                }
                                else
                                {
                                    flag &= BuyController.sController.mLiveDragHelper.TryAddHandToolObjectsToInventory(destinationInventory);
                                }
                            }
                        }
                        mFamilyInventory.InventoryChanged += BuyController.sController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += SetSearchModel;
                        World.OnHandToolRotateToMoveCallback += BuyController.sController.HandToolPickupHandler;
                        if (flag)
                        {
                            if (originWin is not null)
                            {
                                num = BuyController.sController.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            }
                            SetSearchModel();
                            if (string.IsNullOrEmpty(mSearchBar.Query))
                            {
                                BuyController.sController.RepopulateInventory();
                            }
                        }
                    }
                    else
                    {
                        World.OnHandToolRotateToMoveCallback -= BuyController.sController.HandToolPickupHandler;
                        BuyController.sController.mLiveDragHelper.PurgeHandToolObjects();
                        if (originWin is not null)
                        {
                            num = BuyController.sController.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            if (num >= 0)
                            {
                                ZoopBack(BuyController.sController.mDragInfo.mDragOriginStack.TopObject, sender.WindowToScreen(eventArgs.MousePosition), originWin.Parent.WindowToScreen(originWin.Position));
                            }
                        }
                        World.OnHandToolRotateToMoveCallback += BuyController.sController.HandToolPickupHandler;
                    }
                    if (BuyController.sController.mLiveDragHelper.DraggedObjectsCount == 0)
                    {
                        BuyController.sController.mDragInfo = null;
                        return;
                    }
                    if (originWin is not null && num != -1 && BuyController.sController.mCatalogGrid.Items[num].mWin is InventoryItemWin inventoryItemWin)
                    {
                        BuyController.sController.mDragInfo.mDragOriginWin = inventoryItemWin;
                        originWin.ItemDrawState = 1U;
                        originWin.GrabAllHandleWin.DrawState = 1U;
                        originWin.PreviewStackItemCount = BuyController.sController.mDragInfo.mDragOriginStack.Count - BuyController.sController.mLiveDragHelper.DraggedObjectsCount;
                    }
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        private void ZoopBack(ObjectGuid objectGuid, Vector2 startPosition, Vector2 endPosition)
        {
            if (BuyController.sController.mZoopWindow is null)
            {
                InventoryItemWin zoopWindow = UIManager.LoadLayout(ResourceKey.CreateUILayoutKey("HudInventoryItemWin", 0U)).GetWindowByExportID(1) as InventoryItemWin;
                BuyController.sController.mZoopWindow = zoopWindow;
                zoopWindow.BackgroundWin.Visible = false;
                zoopWindow.Thumbnail = objectGuid;
                zoopWindow.Position = startPosition;
                GlideEffect glideEffect = new()
                {
                    TriggerType = EffectBase.TriggerTypes.Manual,
                    Duration = 0.2f,
                    InterpolationType = EffectBase.InterpolationTypes.EaseInOut,
                    EaseTimes = new Vector2(0.2f, 0f),
                    Offset = endPosition - startPosition
                };
                zoopWindow.EffectList.Add(glideEffect);
                UIManager.GetUITopWindow().AddChild(zoopWindow);
                glideEffect.TriggerEffect(false);
                TaskEx.Delay(200).ContinueWith(delegate {
                    if (BuyController.sController.mZoopWindow is not null)
                    {
                        BuyController.sController.mZoopWindow.Parent.DestroyChild(BuyController.sController.mZoopWindow);
                        BuyController.sController.mZoopWindow = null;
                    }
                    SetSearchModel();
                    if (string.IsNullOrEmpty(mSearchBar.Query))
                    {
                        BuyController.sController.RepopulateInventory();
                    }
                });
            }
        }

        private void ClearCatalogGrid()
        {
            BuyController.sController.StopGridPopulation();
            foreach (ItemGridCellItem itemGridCellItem in BuyController.sController.mCatalogGrid.Items)
            {
                switch (BuyController.sController.mCurrCatalogType)
                {
                    case BuyController.CatalogType.Collections:
                        BuyController.sController.mBuildCatalogPatternGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                    case not BuyController.CatalogType.Inventory:
                        BuyController.sController.mBuyCatalogGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                }
            }
            BuyController.sController.mCatalogGrid.Clear();
            BuyController.sController.mNumObjectsInGrid = 0;
        }

        private void SetSearchBarLocation()
        {
            BuyController.CatalogType catalogType = BuyController.sController.mCurrCatalogType;
            float x, y = -35, width;

            if (catalogType == BuyController.CatalogType.ByRoom)
            {
                x = 725;
                width = MathUtils.Clamp(25 + (65 * BuyController.sController.mCatalogGrid.VisibleColumns), 165, 250);
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
                    mSearchBar.Visible = mFamilyInventory is not null;
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
                mDocuments = BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory
                    ? mFamilyInventory.InventoryItems.Select(SelectProduct)
                    : BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                       .Cast<object>()
                                                                       .Select(SelectProduct);
                SearchModel = new TFIDF<object>(mDocuments)
                {
                    Yielding = true
                };

                SearchModel.Preprocess();

                if (!string.IsNullOrEmpty(mSearchBar.Query))
                {
                    ClearCatalogGrid();
                    mPendingQueryTask = TaskEx.Run(QueryEnteredTask);
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
                    if (description == Localization.LocalizeString(kWallDescriptionHash) || description == Localization.LocalizeString(kFloorDescriptionHash))
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
                case IInventoryItemStack iis:
                    name = GameObject.GetObject(iis.TopObject).ToTooltipString();
                    description = "";
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
            Grid grid = BuyController.sController.mGridInventory.Grid.InternalGrid;
            if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                SetSearchModel();
                if (!mInventoryEventRegistered)
                {
                    grid.DragDrop -= BuyController.sController.OnGridDragDrop;
                    grid.DragDrop += OnGridDragDrop;
                    grid.DragEnd -= BuyController.sController.OnGridDragEnd;
                    grid.DragEnd += OnGridDragEnd;
                    mInventoryEventRegistered = true;
                }
            }
            else
            {
                if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections)
                {
                    SetSearchModel();
                }
                if (mInventoryEventRegistered)
                {
                    grid.DragDrop -= OnGridDragDrop;
                    grid.DragEnd -= OnGridDragEnd;
                    mInventoryEventRegistered = false;
                }
            }
        }

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            mSearchBar.Visible = BuyController.sController.mCatalogGrid.Visible;
            if (!string.IsNullOrEmpty(mSearchBar.Query))
            {
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
