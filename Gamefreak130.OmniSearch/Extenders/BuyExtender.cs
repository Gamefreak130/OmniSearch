namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class BuyExtender : BuildBuyExtender
    {
        private readonly IInventory mFamilyInventory;

        private readonly CASCompositorController mCASCompositorController;

        private readonly BuyController mController;

        private bool mInventoryEventRegistered;

        protected override IEnumerable<object> Materials => BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory
                                                                ? mFamilyInventory.InventoryItems
                                                                                  .Cast<object>()
                                                                : BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                                                                   .Cast<object>();

        protected override bool IsSearchBarVisible 
            => mController.mCurrCatalogType switch
            {
                BuyController.CatalogType.ByRoom or BuyController.CatalogType.ByCategory  => mController.mCatalogGrid.Visible && mController.mMiddlePuckWin.Visible,
                BuyController.CatalogType.Collections                                     => mController.mCollectionCatalogWindow.Visible,
                _                                                                         => true
            };

        public BuyExtender() : base(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0))
        {
            mController = BuyController.sController;

            mFamilyInventory = mController.mFamilyInventory;
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged += ResetSearchModel;
            }

            SearchBar.MoveToBack();
            RefreshSearchBar();
            bool defaultInventory = mController.mCurrCatalogType is BuyController.CatalogType.Inventory;
            if (defaultInventory || mController.mPopulateGridTaskHelper is not null)
            {
                if (defaultInventory)
                {
                    RegisterInventoryEvents();
                }
                ResetSearchModel();
            }

            mCASCompositorController = CASCompositorController.Instance;
            mCASCompositorController.ExitFullEditMode += ProcessExistingQuery;

            mController.mTabContainerSortByFunction.TabSelect += OnTabSelect;

            foreach (BuyController.RoomCatalogButtonInfo roomInfo in BuyController.mRoomCatalogButtonInfoDict.Values)
            {
                if (mController.GetChildByID(roomInfo.mButtonId, true) is Button button)
                {
                    button.Click += OnRoomSelect;
                }
            }

            uint num = 2;
            WindowBase childByIndex = mController.mCategorySelectionPanel.GetChildByIndex(num);
            while (childByIndex is not null)
            {
                if (childByIndex is Button button)
                {
                    button.Click += OnCategoryButtonClick;
                }
                childByIndex = mController.mCategorySelectionPanel.GetChildByIndex(num += 1);
            }

            mController.mCatalogCategoryModeButton.Click += OnCatalogButtonClick;
            mController.mCatalogRoomModeButton.Click += OnCatalogButtonClick;
            mController.mCatalogInventoryModeButton.Click += OnCatalogButtonClick;
            mController.mCatalogStoreNotableModeButton.Click += OnCatalogButtonClick;
            mController.mCollectionButton.Click += OnCatalogButtonClick;
            mController.mButtonShopModeSortByRoom.Click += OnCatalogButtonClick;
            mController.mButtonShopModeSortByFunction.Click += OnCatalogButtonClick;
            mController.mButtonShopModeNotable.Click += OnCatalogButtonClick;
            mController.mButtonShopModeEmpty.Click += OnCatalogButtonClick;
            mController.mCategorySelectionPanel.GetChildByID((uint)BuyController.ControlID.LastCategoryTypeButton, true).VisibilityChange += (_,_) => TaskEx.Run(RefreshSearchBar);

            mController.mCollectionGrid.ItemClicked += OnCollectionItemClick;
            mController.mCatalogProductFilter.FiltersChanged += ResetSearchModel;

            mController.mGridSortByRoom.AreaChange += (_,_) => TaskEx.Run(RefreshSearchBar);
            mController.mGridSortByRoom.Grid.VisibilityChange += OnCatalogGridToggled;
            mController.mCollectionCatalogWindow.VisibilityChange += OnCatalogGridToggled;
            mController.mGridSortByFunction.Grid.VisibilityChange += OnCatalogGridToggled;

            mController.mMiddlePuckWin.VisibilityChange += (_,_) => TaskEx.Run(RefreshSearchBar);
        }

        public override void Dispose()
        {
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged -= ResetSearchModel;
            }
            mCASCompositorController.ExitFullEditMode -= ProcessExistingQuery;
            mController.mCatalogProductFilter.FiltersChanged -= ResetSearchModel;
            mController.mTabContainerSortByFunction.TabSelect -= OnTabSelect;
            mController.mCollectionGrid.ItemClicked -= OnCollectionItemClick;
            base.Dispose();
        }

        protected override void ProcessResultsTask(IEnumerable<object> results)
        {
            if (mController.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                PopulateInventory(results);
            }
            else
            {
                mController.PopulateGrid(results, mController.mCurrCatalogType is BuyController.CatalogType.Collections ? BuyController.kBuildCatalogPatternItemKey : BuyController.kBuyCatalogItemKey);
            }
        }

        protected override ISearchModel<object> GetSearchModel() 
            => mController.mCurrCatalogType is BuyController.CatalogType.Inventory ? new ExactMatch<object>(Corpus) : base.GetSearchModel();

        private void PopulateInventory(IEnumerable results)
        {
            int num = mController.mCatalogGrid.GetFirstVisibleItem();

            foreach (IInventoryItemStack stack in results)
            {
                InventoryItemWin inventoryItemWin = InventoryItemWin.MakeEmptySlot();
                inventoryItemWin.Thumbnail = stack.TopObject;
                inventoryItemWin.StackItemCount = stack.Count;
                inventoryItemWin.InUse = stack.InUse;
                if (inventoryItemWin.StackItemCount > 1)
                {
                    inventoryItemWin.GrabAllHandleWin.MouseDown += mController.OnGrabAllHandleWinMouseDown;
                    inventoryItemWin.GrabAllHandleWin.MouseUp += mController.OnGrabAllHandleWinMouseUp;
                    inventoryItemWin.GrabAllHandleWin.DragEnter += mController.OnGridDragEnter;
                    inventoryItemWin.GrabAllHandleWin.DragLeave += mController.OnGridDragLeave;
                    inventoryItemWin.GrabAllHandleWin.DragDrop += OnGridDragDrop;
                    inventoryItemWin.GrabAllHandleWin.DragOver += mController.OnGridDragOver;
                    inventoryItemWin.GrabAllHandleWin.FocusAcquired += mController.OnGrabAllHandleFocusAcquired;
                    inventoryItemWin.GrabAllHandleWin.FocusLost += mController.OnGrabAllHandleFocusLost;
                }
                inventoryItemWin.Tag = stack;
                mController.mCatalogGrid.AddItem(new(inventoryItemWin, stack));
            }

            if (mFamilyInventory.Count > 0)
            {
                mController.mFamilyInventoryInstructionText.Visible = false;
                mController.mFamilyInventorySellAllButton.Enabled = mFamilyInventory.Count == mController.mCatalogGrid.Count;
            }
            else
            {
                mController.mFamilyInventoryInstructionText.Visible = true;
                mController.mFamilyInventorySellAllButton.Enabled = false;
            }
            if (num >= 0)
            {
                num += mController.mCatalogGrid.InternalGrid.ColumnCount;
                if (num > mController.mCatalogGrid.Count)
                {
                    num = mController.mCatalogGrid.Count - 1;
                }
                mController.mCatalogGrid.MakeItemVisible(num);
            }
            mController.ResizeCatalogGrid(mController.mWindowFamilyInventory, mController.mGridInventory, mController.mDefaultInventoryWidth, mController.mDefaultInventoryGridColumns, 0x7FFFFFFE, false);
        }

        private void OnGridDragDrop(WindowBase sender, UIDragEventArgs eventArgs)
        {
            try
            {
                if (eventArgs.Data is LiveDragData)
                {
                    mController.mbDragging = false;
                    int num = mController.mCatalogGrid.GetItemIndexAtPosition(mController.mCatalogGrid.ScreenToWindow(sender.WindowToScreen(eventArgs.MousePosition)));
                    if (num >= 0 && !string.IsNullOrEmpty(SearchBar.Query))
                    {
                        num = mFamilyInventory.InventoryItems.IndexOf(mController.mCatalogGrid.Items[num].mTag as IInventoryItemStack);
                    }
                    if (mController.mInsertionType is InventoryItemWin.InsertionType.After)
                    {
                        num++;
                    }
                    bool flag = mController.mInsertionType is not InventoryItemWin.InsertionType.None;
                    bool result;
                    if (mController.mDragInfo?.mDragOriginatingInventory is IInventory originatingInventory)
                    {
                        mController.mDragInfo.mDragDestinationInventory = mFamilyInventory;
                        if (originatingInventory == mController.mDragInfo.mDragDestinationInventory && (mController.mDragInfo.mbDragOriginIsStack || mController.mDragInfo.mDragOriginStack.Count == 1))
                        {
                            int num2 = originatingInventory.InventoryItems.IndexOf(mController.mDragInfo.mDragOriginStack);
                            if (num2 == num && mController.mInsertionType is InventoryItemWin.InsertionType.Stack)
                            {
                                mController.mInsertionType = InventoryItemWin.InsertionType.Before;
                            }
                        }
                        mController.mDragInfo.mDragDestinationIndex = num;
                        mController.mDragInfo.mbDragDestinationInsert = flag;
                        result = true;
                    }
                    else
                    {
                        result = true;
                        mFamilyInventory.InventoryChanged -= mController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= ResetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= mController.HandToolPickupHandler;
                        if (flag)
                        {
                            result = mController.AddDraggedObjectsToFamilyInventory(num);
                            if (result)
                            {
                                mController.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        else if (mController.mLiveDragHelper.DragIsFromInventoryToInventory())
                        {
                            eventArgs.Result = mController.mLiveDragHelper.TryAddHandToolObjectsToInventory(mFamilyInventory);
                        }
                        else
                        {
                            result = mController.AddDraggedObjectsToFamilyInventory(-1);
                            if (result)
                            {
                                mController.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        mFamilyInventory.InventoryChanged += mController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += ResetSearchModel;
                        World.OnHandToolRotateToMoveCallback += mController.HandToolPickupHandler;
                        ResetSearchModel();
                        if (string.IsNullOrEmpty(SearchBar.Query))
                        {
                            mController.RepopulateInventory();
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
                    mController.mbDragging = false;
                    int num = -1;
                    InventoryItemWin originWin = mController.mDragInfo?.mDragOriginWin;
                    mController.mFamilyInventorySellAllButton.Visible = true;
                    mController.mFamilyInventorySellButton.Visible = false;
                    if (eventArgs.Result)
                    {
                        bool flag = true;
                        mFamilyInventory.InventoryChanged -= mController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= ResetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= mController.HandToolPickupHandler;
                        if (mController.mDragInfo is not null)
                        {
                            IInventory destinationInventory = mController.mDragInfo.mDragDestinationInventory;
                            if (mController.mDragInfo.mDragOriginStack is IInventoryItemStack originStack && mController.mDragInfo.mDragOriginatingInventory is IInventory originatingInventory)
                            {
                                int num2 = originatingInventory.InventoryItems.IndexOf(originStack);
                                int count = originatingInventory.InventoryItems.Count;
                                if (mController.mDragInfo.mbDragOriginIsStack && destinationInventory is not null)
                                {
                                    flag &= originatingInventory.TryRemoveStackFromInventory(originStack);
                                }
                                else
                                {
                                    flag &= originatingInventory.TryRemoveTopObjectFromStack(originStack);
                                }
                                if (flag && destinationInventory == originatingInventory && mController.mDragInfo.mbDragDestinationInsert && num2 < mController.mDragInfo.mDragDestinationIndex)
                                {
                                    mController.mDragInfo.mDragDestinationIndex -= count - originatingInventory.InventoryItems.Count;
                                }
                            }
                            if (destinationInventory is not null)
                            {
                                if (mController.mDragInfo.mbDragDestinationInsert)
                                {
                                    flag &= destinationInventory.TryInsertNewStackAt(mController.mLiveDragHelper.DraggedObjects, mController.mDragInfo.mDragDestinationIndex, mController.mInsertionType == InventoryItemWin.InsertionType.Stack);
                                    mController.mLiveDragHelper.PurgeHandToolObjects();
                                }
                                else
                                {
                                    flag &= mController.mLiveDragHelper.TryAddHandToolObjectsToInventory(destinationInventory);
                                }
                            }
                        }
                        mFamilyInventory.InventoryChanged += mController.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += ResetSearchModel;
                        World.OnHandToolRotateToMoveCallback += mController.HandToolPickupHandler;
                        if (flag)
                        {
                            if (originWin is not null)
                            {
                                num = mController.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            }
                            ResetSearchModel();
                            if (string.IsNullOrEmpty(SearchBar.Query))
                            {
                                mController.RepopulateInventory();
                            }
                        }
                    }
                    else
                    {
                        World.OnHandToolRotateToMoveCallback -= mController.HandToolPickupHandler;
                        mController.mLiveDragHelper.PurgeHandToolObjects();
                        if (originWin is not null)
                        {
                            num = mController.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            if (num >= 0)
                            {
                                ZoopBack(mController.mDragInfo.mDragOriginStack.TopObject, sender.WindowToScreen(eventArgs.MousePosition), originWin.Parent.WindowToScreen(originWin.Position));
                            }
                        }
                        World.OnHandToolRotateToMoveCallback += mController.HandToolPickupHandler;
                    }
                    if (mController.mLiveDragHelper.DraggedObjectsCount == 0)
                    {
                        mController.mDragInfo = null;
                        return;
                    }
                    if (originWin is not null && num != -1 && mController.mCatalogGrid.Items[num].mWin is InventoryItemWin inventoryItemWin)
                    {
                        mController.mDragInfo.mDragOriginWin = inventoryItemWin;
                        originWin.ItemDrawState = 1U;
                        originWin.GrabAllHandleWin.DrawState = 1U;
                        originWin.PreviewStackItemCount = mController.mDragInfo.mDragOriginStack.Count - mController.mLiveDragHelper.DraggedObjectsCount;
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
            if (mController.mZoopWindow is null)
            {
                InventoryItemWin zoopWindow = UIManager.LoadLayout(ResourceKey.CreateUILayoutKey("HudInventoryItemWin", 0U)).GetWindowByExportID(1) as InventoryItemWin;
                mController.mZoopWindow = zoopWindow;
                zoopWindow.BackgroundWin.Visible = false;
                zoopWindow.Thumbnail = objectGuid;
                zoopWindow.Position = startPosition;
                GlideEffect glideEffect = new()
                {
                    TriggerType = EffectBase.TriggerTypes.Manual,
                    Duration = 0.2f,
                    InterpolationType = EffectBase.InterpolationTypes.EaseInOut,
                    EaseTimes = new(0.2f, 0f),
                    Offset = endPosition - startPosition
                };
                zoopWindow.EffectList.Add(glideEffect);
                UIManager.GetUITopWindow().AddChild(zoopWindow);
                glideEffect.TriggerEffect(false);
                TaskEx.Delay(200).ContinueWith(delegate {
                    if (mController.mZoopWindow is not null)
                    {
                        mController.mZoopWindow.Parent.DestroyChild(mController.mZoopWindow);
                        mController.mZoopWindow = null;
                    }
                    ResetSearchModel();
                    if (string.IsNullOrEmpty(SearchBar.Query))
                    {
                        mController.RepopulateInventory();
                    }
                });
            }
        }

        protected override void ClearItems()
        {
            mController.StopGridPopulation();
            foreach (ItemGridCellItem itemGridCellItem in mController.mCatalogGrid.Items)
            {
                switch (mController.mCurrCatalogType)
                {
                    case BuyController.CatalogType.Collections:
                        mController.mBuildCatalogPatternGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                    case not BuyController.CatalogType.Inventory:
                        mController.mBuyCatalogGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                }
            }
            mController.mCatalogGrid.Clear();
            mController.mNumObjectsInGrid = 0;
        }

        protected override void RefreshSearchBar()
        {
            SetSearchBarVisibility();
            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
            }
        }

        protected override void SetSearchBarLocation()
        {
            float x, y = -35, width;

            if (mController.mCurrCatalogType == BuyController.CatalogType.ByRoom)
            {
                x = 725;
                width = MathUtils.Clamp(25 + (65 * mController.mCatalogGrid.VisibleColumns), 165, 250);
            }
            else
            {
                width = mController.mCurrCatalogType == BuyController.CatalogType.ByCategory && BuyController.sBuyDebug ? 201 : 251;
                if (mController.mBuyModel.IsInShopMode())
                {
                    x = 362;
                    width -= 45;
                }
                else
                {
                    x = 317;
                }
            }

            SearchBar.SetLocation(x, y, width);
        }

        private void RegisterInventoryEvents()
        {
            if (!mInventoryEventRegistered)
            {
                Grid grid = mController.mGridInventory.Grid.InternalGrid;
                grid.DragDrop -= mController.OnGridDragDrop;
                grid.DragDrop += OnGridDragDrop;
                grid.DragEnd -= mController.OnGridDragEnd;
                grid.DragEnd += OnGridDragEnd;
                mInventoryEventRegistered = true;
            }
        }

        private void UnregisterInventoryEvents()
        {
            if (mInventoryEventRegistered)
            {
                Grid grid = mController.mGridInventory.Grid.InternalGrid;
                grid.DragDrop -= OnGridDragDrop;
                grid.DragEnd -= OnGridDragEnd;
                mInventoryEventRegistered = false;
            }
        }

        private void OnTabSelect(TabControl _, TabControl __)
        {
            RefreshSearchBar();
            ResetSearchModel();
        }

        private void OnCollectionItemClick(ItemGrid _, ItemGridCellClickEvent __) => ResetSearchModel();

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            SearchBar.Clear();
            RefreshSearchBar();
            if (mController.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                ResetSearchModel();
                RegisterInventoryEvents();
            }
            else
            {
                if (mController.mCurrCatalogType is BuyController.CatalogType.Collections or BuyController.CatalogType.StoreNotable)
                {
                    ResetSearchModel();
                }
                UnregisterInventoryEvents();
            }
        }

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => ResetSearchModel();

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            RefreshSearchBar();
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearItems();
            }
        }

        private void OnCatalogGridToggled(WindowBase _, UIVisibilityChangeEventArgs args)
        {
            RefreshSearchBar();
            SearchBar.Clear();
            ResetSearchModel();
        }
    }
}
