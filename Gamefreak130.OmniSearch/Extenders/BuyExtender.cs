using Sims3.UI.CAS;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // TODO exactmatch for inventory, wall/floor, world editor searches?
    public class BuyExtender : BuildBuyExtender
    {
        private readonly IInventory mFamilyInventory;

        private readonly CASCompositorController mCASCompositorController;

        private bool mInventoryEventRegistered;

        protected override IEnumerable<object> Materials => BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory
                                                                ? mFamilyInventory.InventoryItems
                                                                                  .Cast<object>()
                                                                : BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                                                                   .Cast<object>();

        public BuyExtender() : base(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0))
        {
            BuyController controller = BuyController.sController;

            mFamilyInventory = controller.mFamilyInventory;
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged += SetSearchModel;
            }

            SearchBar.MoveToBack();
            bool defaultInventory = controller.mCurrCatalogType is BuyController.CatalogType.Inventory;
            if (defaultInventory || controller.mPopulateGridTaskHelper is not null)
            {
                if (defaultInventory)
                {
                    RegisterInventoryEvents();
                }
                SetSearchModel();
            }

            mCASCompositorController = CASCompositorController.Instance;
            mCASCompositorController.ExitFullEditMode += ProcessExistingQuery;

            controller.mTabContainerSortByFunction.TabSelect += OnTabSelect;

            foreach (BuyController.RoomCatalogButtonInfo roomInfo in BuyController.mRoomCatalogButtonInfoDict.Values)
            {
                if (controller.GetChildByID(roomInfo.mButtonId, true) is Button button)
                {
                    button.Click += OnRoomSelect;
                }
            }

            uint num = 2;
            WindowBase childByIndex = controller.mCategorySelectionPanel.GetChildByIndex(num);
            while (childByIndex is not null)
            {
                if (childByIndex is Button button)
                {
                    button.Click += OnCategoryButtonClick;
                }
                childByIndex = controller.mCategorySelectionPanel.GetChildByIndex(num += 1);
            }

            controller.mCatalogCategoryModeButton.Click += OnCatalogButtonClick;
            controller.mCatalogRoomModeButton.Click += OnCatalogButtonClick;
            controller.mCatalogInventoryModeButton.Click += OnCatalogButtonClick;
            controller.mCatalogStoreNotableModeButton.Click += OnCatalogButtonClick;
            controller.mCollectionButton.Click += OnCatalogButtonClick;
            controller.mButtonShopModeSortByRoom.Click += OnCatalogButtonClick;
            controller.mButtonShopModeSortByFunction.Click += OnCatalogButtonClick;
            controller.mButtonShopModeNotable.Click += OnCatalogButtonClick;
            controller.mButtonShopModeEmpty.Click += OnCatalogButtonClick;
            controller.mCategorySelectionPanel.GetChildByID((uint)BuyController.ControlID.LastCategoryTypeButton, true).VisibilityChange += (_,_) => TaskEx.Run(RefreshSearchBarVisibility);

            controller.mCollectionGrid.ItemClicked += (_,_) => SetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += SetSearchModel;

            controller.mGridSortByRoom.AreaChange += (_,_) => TaskEx.Run(RefreshSearchBarVisibility);
            controller.mGridSortByRoom.Grid.VisibilityChange += OnCatalogGridToggled;
            controller.mCollectionCatalogWindow.VisibilityChange += OnCatalogGridToggled;
            controller.mGridSortByFunction.Grid.VisibilityChange += OnCatalogGridToggled;

            controller.mMiddlePuckWin.VisibilityChange += (_,_) => TaskEx.Run(RefreshSearchBarVisibility);
        }

        public override void Dispose()
        {
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged -= SetSearchModel;
            }
            mCASCompositorController.ExitFullEditMode -= ProcessExistingQuery;
            base.Dispose();
        }

        protected override void ProcessResultsTask(IEnumerable<object> results)
        {
            BuyController controller = BuyController.sController;
            if (controller.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                PopulateInventory(results);
            }
            else
            {
                controller.PopulateGrid(results, controller.mCurrCatalogType is BuyController.CatalogType.Collections ? BuyController.kBuildCatalogPatternItemKey : BuyController.kBuyCatalogItemKey);
            }
        }

        private void PopulateInventory(IEnumerable results)
        {
            BuyController controller = BuyController.sController;
            int num = controller.mCatalogGrid.GetFirstVisibleItem();

            foreach (IInventoryItemStack stack in results)
            {
                InventoryItemWin inventoryItemWin = InventoryItemWin.MakeEmptySlot();
                inventoryItemWin.Thumbnail = stack.TopObject;
                inventoryItemWin.StackItemCount = stack.Count;
                inventoryItemWin.InUse = stack.InUse;
                if (inventoryItemWin.StackItemCount > 1)
                {
                    inventoryItemWin.GrabAllHandleWin.MouseDown += controller.OnGrabAllHandleWinMouseDown;
                    inventoryItemWin.GrabAllHandleWin.MouseUp += controller.OnGrabAllHandleWinMouseUp;
                    inventoryItemWin.GrabAllHandleWin.DragEnter += controller.OnGridDragEnter;
                    inventoryItemWin.GrabAllHandleWin.DragLeave += controller.OnGridDragLeave;
                    inventoryItemWin.GrabAllHandleWin.DragDrop += OnGridDragDrop;
                    inventoryItemWin.GrabAllHandleWin.DragOver += controller.OnGridDragOver;
                    inventoryItemWin.GrabAllHandleWin.FocusAcquired += controller.OnGrabAllHandleFocusAcquired;
                    inventoryItemWin.GrabAllHandleWin.FocusLost += controller.OnGrabAllHandleFocusLost;
                }
                inventoryItemWin.Tag = stack;
                controller.mCatalogGrid.AddItem(new(inventoryItemWin, stack));
            }

            if (mFamilyInventory.Count > 0)
            {
                controller.mFamilyInventoryInstructionText.Visible = false;
                controller.mFamilyInventorySellAllButton.Enabled = mFamilyInventory.Count == controller.mCatalogGrid.Count;
            }
            else
            {
                controller.mFamilyInventoryInstructionText.Visible = true;
                controller.mFamilyInventorySellAllButton.Enabled = false;
            }
            if (num >= 0)
            {
                num += controller.mCatalogGrid.InternalGrid.ColumnCount;
                if (num > controller.mCatalogGrid.Count)
                {
                    num = controller.mCatalogGrid.Count - 1;
                }
                controller.mCatalogGrid.MakeItemVisible(num);
            }
            controller.ResizeCatalogGrid(controller.mWindowFamilyInventory, controller.mGridInventory, controller.mDefaultInventoryWidth, controller.mDefaultInventoryGridColumns, 0x7FFFFFFE, false);
        }

        private void OnGridDragDrop(WindowBase sender, UIDragEventArgs eventArgs)
        {
            try
            {
                if (eventArgs.Data is LiveDragData)
                {
                    BuyController controller = BuyController.sController;

                    controller.mbDragging = false;
                    int num = controller.mCatalogGrid.GetItemIndexAtPosition(controller.mCatalogGrid.ScreenToWindow(sender.WindowToScreen(eventArgs.MousePosition)));
                    if (num >= 0 && !string.IsNullOrEmpty(SearchBar.Query))
                    {
                        num = mFamilyInventory.InventoryItems.IndexOf(controller.mCatalogGrid.Items[num].mTag as IInventoryItemStack);
                    }
                    if (controller.mInsertionType is InventoryItemWin.InsertionType.After)
                    {
                        num++;
                    }
                    bool flag = controller.mInsertionType is not InventoryItemWin.InsertionType.None;
                    bool result;
                    if (controller.mDragInfo?.mDragOriginatingInventory is IInventory originatingInventory)
                    {
                        controller.mDragInfo.mDragDestinationInventory = mFamilyInventory;
                        if (originatingInventory == controller.mDragInfo.mDragDestinationInventory && (controller.mDragInfo.mbDragOriginIsStack || controller.mDragInfo.mDragOriginStack.Count == 1))
                        {
                            int num2 = originatingInventory.InventoryItems.IndexOf(controller.mDragInfo.mDragOriginStack);
                            if (num2 == num && controller.mInsertionType is InventoryItemWin.InsertionType.Stack)
                            {
                                controller.mInsertionType = InventoryItemWin.InsertionType.Before;
                            }
                        }
                        controller.mDragInfo.mDragDestinationIndex = num;
                        controller.mDragInfo.mbDragDestinationInsert = flag;
                        result = true;
                    }
                    else
                    {
                        result = true;
                        mFamilyInventory.InventoryChanged -= controller.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= SetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= controller.HandToolPickupHandler;
                        if (flag)
                        {
                            result = controller.AddDraggedObjectsToFamilyInventory(num);
                            if (result)
                            {
                                controller.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        else if (controller.mLiveDragHelper.DragIsFromInventoryToInventory())
                        {
                            eventArgs.Result = controller.mLiveDragHelper.TryAddHandToolObjectsToInventory(mFamilyInventory);
                        }
                        else
                        {
                            result = controller.AddDraggedObjectsToFamilyInventory(-1);
                            if (result)
                            {
                                controller.mLiveDragHelper.PurgeHandToolObjects();
                            }
                        }
                        mFamilyInventory.InventoryChanged += controller.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += SetSearchModel;
                        World.OnHandToolRotateToMoveCallback += controller.HandToolPickupHandler;
                        SetSearchModel();
                        if (string.IsNullOrEmpty(SearchBar.Query))
                        {
                            controller.RepopulateInventory();
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
                    BuyController controller = BuyController.sController;

                    controller.mbDragging = false;
                    int num = -1;
                    InventoryItemWin originWin = controller.mDragInfo?.mDragOriginWin;
                    controller.mFamilyInventorySellAllButton.Visible = true;
                    controller.mFamilyInventorySellButton.Visible = false;
                    if (eventArgs.Result)
                    {
                        bool flag = true;
                        mFamilyInventory.InventoryChanged -= controller.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged -= SetSearchModel;
                        World.OnHandToolRotateToMoveCallback -= controller.HandToolPickupHandler;
                        if (controller.mDragInfo is not null)
                        {
                            IInventory destinationInventory = controller.mDragInfo.mDragDestinationInventory;
                            if (controller.mDragInfo.mDragOriginStack is IInventoryItemStack originStack && controller.mDragInfo.mDragOriginatingInventory is IInventory originatingInventory)
                            {
                                int num2 = originatingInventory.InventoryItems.IndexOf(originStack);
                                int count = originatingInventory.InventoryItems.Count;
                                if (controller.mDragInfo.mbDragOriginIsStack && destinationInventory is not null)
                                {
                                    flag &= originatingInventory.TryRemoveStackFromInventory(originStack);
                                }
                                else
                                {
                                    flag &= originatingInventory.TryRemoveTopObjectFromStack(originStack);
                                }
                                if (flag && destinationInventory == originatingInventory && controller.mDragInfo.mbDragDestinationInsert && num2 < controller.mDragInfo.mDragDestinationIndex)
                                {
                                    controller.mDragInfo.mDragDestinationIndex -= count - originatingInventory.InventoryItems.Count;
                                }
                            }
                            if (destinationInventory is not null)
                            {
                                if (controller.mDragInfo.mbDragDestinationInsert)
                                {
                                    flag &= destinationInventory.TryInsertNewStackAt(controller.mLiveDragHelper.DraggedObjects, controller.mDragInfo.mDragDestinationIndex, controller.mInsertionType == InventoryItemWin.InsertionType.Stack);
                                    controller.mLiveDragHelper.PurgeHandToolObjects();
                                }
                                else
                                {
                                    flag &= controller.mLiveDragHelper.TryAddHandToolObjectsToInventory(destinationInventory);
                                }
                            }
                        }
                        mFamilyInventory.InventoryChanged += controller.OnInventoryChanged;
                        mFamilyInventory.InventoryChanged += SetSearchModel;
                        World.OnHandToolRotateToMoveCallback += controller.HandToolPickupHandler;
                        if (flag)
                        {
                            if (originWin is not null)
                            {
                                num = controller.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            }
                            SetSearchModel();
                            if (string.IsNullOrEmpty(SearchBar.Query))
                            {
                                controller.RepopulateInventory();
                            }
                        }
                    }
                    else
                    {
                        World.OnHandToolRotateToMoveCallback -= controller.HandToolPickupHandler;
                        controller.mLiveDragHelper.PurgeHandToolObjects();
                        if (originWin is not null)
                        {
                            num = controller.mCatalogGrid.Items.FindIndex(item => item.mWin == originWin);
                            if (num >= 0)
                            {
                                ZoopBack(controller.mDragInfo.mDragOriginStack.TopObject, sender.WindowToScreen(eventArgs.MousePosition), originWin.Parent.WindowToScreen(originWin.Position));
                            }
                        }
                        World.OnHandToolRotateToMoveCallback += controller.HandToolPickupHandler;
                    }
                    if (controller.mLiveDragHelper.DraggedObjectsCount == 0)
                    {
                        controller.mDragInfo = null;
                        return;
                    }
                    if (originWin is not null && num != -1 && controller.mCatalogGrid.Items[num].mWin is InventoryItemWin inventoryItemWin)
                    {
                        controller.mDragInfo.mDragOriginWin = inventoryItemWin;
                        originWin.ItemDrawState = 1U;
                        originWin.GrabAllHandleWin.DrawState = 1U;
                        originWin.PreviewStackItemCount = controller.mDragInfo.mDragOriginStack.Count - controller.mLiveDragHelper.DraggedObjectsCount;
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
            BuyController controller = BuyController.sController;

            if (controller.mZoopWindow is null)
            {
                InventoryItemWin zoopWindow = UIManager.LoadLayout(ResourceKey.CreateUILayoutKey("HudInventoryItemWin", 0U)).GetWindowByExportID(1) as InventoryItemWin;
                controller.mZoopWindow = zoopWindow;
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
                    if (controller.mZoopWindow is not null)
                    {
                        controller.mZoopWindow.Parent.DestroyChild(controller.mZoopWindow);
                        controller.mZoopWindow = null;
                    }
                    SetSearchModel();
                    if (string.IsNullOrEmpty(SearchBar.Query))
                    {
                        controller.RepopulateInventory();
                    }
                });
            }
        }

        protected override void ClearItems()
        {
            BuyController controller = BuyController.sController;

            controller.StopGridPopulation();
            foreach (ItemGridCellItem itemGridCellItem in controller.mCatalogGrid.Items)
            {
                switch (controller.mCurrCatalogType)
                {
                    case BuyController.CatalogType.Collections:
                        controller.mBuildCatalogPatternGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                    case not BuyController.CatalogType.Inventory:
                        controller.mBuyCatalogGridItemCache.Add(itemGridCellItem.mWin);
                        break;
                }
            }
            controller.mCatalogGrid.Clear();
            controller.mNumObjectsInGrid = 0;
        }

        protected override void SetSearchBarVisibility(bool visible)
        {
            SearchBar.Visible = visible;
            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
            }
        }

        protected override void RefreshSearchBarVisibility()
        {
            if (BuyController.sController is BuyController controller)
            {
                bool visible = controller.mCurrCatalogType switch
                {
                    BuyController.CatalogType.ByRoom or BuyController.CatalogType.ByCategory  => controller.mCatalogGrid.Visible && controller.mMiddlePuckWin.Visible,
                    BuyController.CatalogType.Collections                                     => controller.mCollectionCatalogWindow.Visible,
                    _                                                                         => true
                };

                SetSearchBarVisibility(visible);
            }
        }

        protected override void SetSearchBarLocation()
        {
            BuyController controller = BuyController.sController;
            float x, y = -35, width;

            if (controller.mCurrCatalogType == BuyController.CatalogType.ByRoom)
            {
                x = 725;
                width = MathUtils.Clamp(25 + (65 * controller.mCatalogGrid.VisibleColumns), 165, 250);
            }
            else
            {
                width = controller.mCurrCatalogType == BuyController.CatalogType.ByCategory && BuyController.sBuyDebug ? 201 : 251;
                if (controller.mBuyModel.IsInShopMode())
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
                Grid grid = BuyController.sController.mGridInventory.Grid.InternalGrid;
                grid.DragDrop -= BuyController.sController.OnGridDragDrop;
                grid.DragDrop += OnGridDragDrop;
                grid.DragEnd -= BuyController.sController.OnGridDragEnd;
                grid.DragEnd += OnGridDragEnd;
                mInventoryEventRegistered = true;
            }
        }

        private void UnregisterInventoryEvents()
        {
            if (mInventoryEventRegistered)
            {
                Grid grid = BuyController.sController.mGridInventory.Grid.InternalGrid;
                grid.DragDrop -= OnGridDragDrop;
                grid.DragEnd -= OnGridDragEnd;
                mInventoryEventRegistered = false;
            }
        }

        private void OnTabSelect(TabControl _, TabControl __)
        {
            RefreshSearchBarVisibility();
            SetSearchModel();
        }

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            SearchBar.Clear();
            RefreshSearchBarVisibility();
            if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                SetSearchModel();
                RegisterInventoryEvents();
            }
            else
            {
                if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections or BuyController.CatalogType.StoreNotable)
                {
                    SetSearchModel();
                }
                UnregisterInventoryEvents();
            }
        }

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            RefreshSearchBarVisibility();
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearItems();
            }
        }

        private void OnCatalogGridToggled(WindowBase _, UIVisibilityChangeEventArgs args)
        {
            RefreshSearchBarVisibility();
            SearchBar.Clear();
            SetSearchModel();
        }
    }
}
