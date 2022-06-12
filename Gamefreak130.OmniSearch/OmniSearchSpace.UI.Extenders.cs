using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI.CAS;
using Sims3.UI.Hud;
using Sims3.UI.Store;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO Fix shop mode weirdness
    // TEST featured store items
    // TEST resort build/buy
    // TEST interior design
    public abstract class SearchExtender<TDocument, TResult> : IDisposable
    {
        private ISearchModel<TResult> mSearchModel;

        protected virtual ISearchModel<TResult> SearchModel
        {
            get => mSearchModel;
            set
            {
                mSearchModel?.Dispose();
                mSearchModel = value;
                SearchModel?.Preprocess();
            }
        }

        protected OmniSearchBar SearchBar { get; private set; }

        protected abstract IEnumerable<TDocument> Corpus { get; }

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup) 
            => SearchBar = new(searchBarGroup, parentWindow, QueryEnteredTask);

        public virtual void Dispose()
        {
            SearchBar.Dispose();
            SearchModel = null;
        }

        protected abstract void QueryEnteredTask();
    }

    public abstract class TitleDescriptionSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected TitleDescriptionSearchExtender(WindowBase parentWindow, string searchBarGroup) : base(parentWindow, searchBarGroup)
        {
        }

        protected abstract Document<T> SelectDocument(T obj);
    }

    public abstract class BuildBuyExtender : TitleDescriptionSearchExtender<object>
    {
        protected const ulong kWallDescriptionHash = 0xDD1EAD49D9F75762;

        protected const ulong kFloorDescriptionHash = 0x2DE87A7A181E89C4;

        private IEnumerable<Document<object>> mDocuments;

        protected BuildBuyExtender(WindowBase parentWindow) : base(parentWindow, "BuildBuy")
            => EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                    Dispose();
                    return ListenerAction.Remove;
                });

        public override void Dispose()
        {
            // TODO Cleanup if not needed
            base.Dispose();
        }

        protected override Document<object> SelectDocument(object product)
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
                    throw new ArgumentException($"{product.GetType()} is not a valid Build/Buy product", nameof(product));
            }

            return new Document<object>(name, description, product);
        }

        protected void SetSearchModel()
        {
            try
            {
                mDocuments = Corpus;
                SearchModel = new TFIDF<object>(mDocuments)
                {
                    Yielding = true
                };

                ProcessExistingQuery();
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected void ProcessExistingQuery()
        {
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearCatalogGrid();
                SearchBar.TriggerSearch();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected IEnumerable<object> SearchCollections()
        {
            ITokenizer tokenizer = Tokenizer.Create();
            foreach (IBBCollectionData collection in Responder.Instance.BuildModel.CollectionInfo.CollectionData)
            {
                if (tokenizer.Tokenize(SearchBar.Query).SequenceEqual(tokenizer.Tokenize(collection.CollectionName)))
                {
                    List<Document<object>> collectionDocs = collection.Items.ConvertAll(SelectDocument);
                    IEnumerable<object> collectionProducts = from document in mDocuments
                                                             where collectionDocs.Contains(document)
                                                             select document.Tag;

                    if (collectionProducts.FirstOrDefault() is not null)
                    {
                        return collectionProducts;
                    }
                }
            }
            return null;
        }

        protected abstract void ClearCatalogGrid();
    }

    public class BuyExtender : BuildBuyExtender
    {
        private readonly IInventory mFamilyInventory;

        private bool mInventoryEventRegistered;

        protected override IEnumerable<Document<object>> Corpus => BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory
                                                                ? mFamilyInventory.InventoryItems
                                                                                  .Select(SelectDocument)
                                                                : BuyController.sController.mPopulateGridTaskHelper.Collection
                                                                                                                   .Cast<object>()
                                                                                                                   .Select(SelectDocument);

        public BuyExtender() : base(BuyController.sLayout.GetWindowByExportID(1).GetChildByIndex(0))
        {
            BuyController controller = BuyController.sController;

            mFamilyInventory = controller.mFamilyInventory;
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged += SetSearchModel;
            }

            SearchBar.MoveToBack();
            SetSearchBarVisibility();
            bool defaultInventory = controller.mCurrCatalogType is BuyController.CatalogType.Inventory;
            if (defaultInventory || controller.mPopulateGridTaskHelper is not null)
            {
                if (defaultInventory)
                {
                    RegisterInventoryEvents();
                }
                SetSearchModel();
            }

            CASCompositorController.Instance.ExitFullEditMode += ProcessExistingQuery;

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
            controller.mCollectionButton.Click += OnCatalogButtonClick;
            controller.mButtonShopModeSortByRoom.Click += OnCatalogButtonClick;
            controller.mButtonShopModeSortByFunction.Click += OnCatalogButtonClick;
            controller.mButtonShopModeNotable.Click += OnCatalogButtonClick;
            controller.mButtonShopModeEmpty.Click += OnCatalogButtonClick;
            controller.mCategorySelectionPanel.GetChildByID((uint)BuyController.ControlID.LastCategoryTypeButton, true).VisibilityChange += (_,_) => TaskEx.Run(SetSearchBarVisibility);

            controller.mCollectionGrid.ItemClicked += (_,_) => SetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += SetSearchModel;

            controller.mGridSortByRoom.AreaChange += (_,_) => TaskEx.Run(SetSearchBarVisibility);
            controller.mGridSortByRoom.Grid.VisibilityChange += OnCatalogGridToggled;
            controller.mCollectionCatalogWindow.VisibilityChange += OnCatalogGridToggled;
            controller.mGridSortByFunction.Grid.VisibilityChange += OnCatalogGridToggled;

            controller.mMiddlePuckWin.VisibilityChange += (_, args) => SearchBar.Visible = args.Visible;
        }

        public override void Dispose()
        {
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged -= SetSearchModel;
            }
            base.Dispose();
        }

        protected override void QueryEnteredTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));
                IEnumerable results = null;
                BuyController controller = BuyController.sController;

                if (controller.mCurrCatalogType is not (BuyController.CatalogType.Collections or BuyController.CatalogType.Inventory))
                {
                    results = SearchCollections();
                }
#if DEBUG
                results ??= SearchModel.Search(SearchBar.Query)
                                       .ToList();

                //DocumentLogger.sInstance.WriteLog();
#else
                results ??= SearchModel.Search(SearchBar.Query);
#endif

                ClearCatalogGrid();
                if (controller.mCurrCatalogType is BuyController.CatalogType.Inventory)
                {
                    PopulateInventory(results);
                }
                else
                {
                    controller.PopulateGrid(results, controller.mCurrCatalogType is BuyController.CatalogType.Collections ? BuyController.kBuildCatalogPatternItemKey : BuyController.kBuyCatalogItemKey);
                }
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
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
                    EaseTimes = new Vector2(0.2f, 0f),
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

        protected override void ClearCatalogGrid()
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

        private void SetSearchBarVisibility()
        {
            if (BuyController.sController is BuyController controller)
            {
                BuyController.CatalogType catalogType = controller.mCurrCatalogType;
                float x, y = -35, width;

                if (catalogType == BuyController.CatalogType.ByRoom)
                {
                    x = 725;
                    width = MathUtils.Clamp(25 + (65 * controller.mCatalogGrid.VisibleColumns), 165, 250);
                }
                else
                {
                    width = catalogType == BuyController.CatalogType.ByCategory && BuyController.sBuyDebug ? 201 : 251;
                    if (controller.mBuyModel.IsInShopMode())
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
                        SearchBar.Visible = mFamilyInventory is not null;
                    }
                    if (catalogType is BuyController.CatalogType.Collections)
                    {
                        SearchBar.Visible = controller.mCollectionCatalogWindow.Visible;
                    }
                }
                SearchBar.SetLocation(x, y, width);
            }
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
            SetSearchBarVisibility();
            SetSearchModel();
        }

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            SearchBar.Clear();
            SetSearchBarVisibility();
            if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory)
            {
                SetSearchModel();
                RegisterInventoryEvents();
            }
            else
            {
                if (BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Collections)
                {
                    SetSearchModel();
                }
                UnregisterInventoryEvents();
            }
        }

        private void OnRoomSelect(WindowBase _, UIButtonClickEventArgs __) => SetSearchModel();

        private void OnCategoryButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            SearchBar.Visible = BuyController.sController.mCatalogGrid.Visible;
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearCatalogGrid();
            }
        }

        private void OnCatalogGridToggled(WindowBase _, UIVisibilityChangeEventArgs args)
        {
            SearchBar.Visible = args.Visible;
            SearchBar.Clear();
            SetSearchModel();
        }
    }
    // CONSIDER BuildCatalogItem vs. BuildPatternItem in search results
    public class BuildExtender : BuildBuyExtender
    {
        private bool mCompositorActive;

        private bool mSellPanelActive;

        protected override IEnumerable<Document<object>> Corpus => (BuildController.sController.mProductList ?? BuildController.sController.mPresetList).Select(SelectDocument);

        public BuildExtender() : base(BuildController.sLayout.GetWindowByExportID(1).GetChildByIndex(2))
        {
            SetSearchBarVisibility();
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

            controller.mCollectionGrid.ItemClicked += (_, _) => SetSearchModel();
            controller.mCatalogProductFilter.FiltersChanged += SetSearchModel;

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

        protected override void ClearCatalogGrid()
        {
            BuildController.sController.mCurrentCatalogGrid.AbortPopulating();
            BuildController.sController.mCurrentCatalogGrid.Clear();
        }

        private void SetSearchBarVisibility()
        {
            BuildController controller = BuildController.sController;
            SearchBar.Visible = controller.mMiddlePuckWin.Visible && controller.mCurrentCatalogGrid is not null && (!BuildController.sCollectionMode || controller.mCollectionCatalogWindow.Visible);

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
                    SetSearchModel();
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

        private void SetSearchBarLocation()
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

        private void OnCategorySelected(object _, EventArgs __)
        {
            SearchBar.Clear();
            SetSearchBarVisibility();
        }

        private void OnTabSelect(TabControl _, TabControl __) => SetSearchModel();

        private void OnMiddlePuckVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __) => SetSearchBarVisibility();

        private void OnEyedropperPick(object _, EventArgs __)
        {
            OnCategorySelected(_, __);
            ClearCatalogGrid();
            SearchBar.TriggerSearch();
        }

        protected override void QueryEnteredTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));
                BuildController controller = BuildController.sController;
                List<object> results = null;
                if (!controller.mCollectionWindow.Visible)
                {
                    results = SearchCollections()?.ToList();
                }

                results ??= SearchModel.Search(SearchBar.Query)
                                       .ToList();

#if DEBUG
                //DocumentLogger.sInstance.WriteLog();
#endif

                ClearCatalogGrid();
                

                List<object> productList = controller.mProductList is not null ? results : null,
                             presetList = controller.mPresetList is not null ? results : null;

                controller.PopulateCatalogGrid(controller.mCurrentCatalogGrid, "BuildCatalogItem", productList, presetList, controller.mCatalogProduct, controller.mCatalogFilter);
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
            }
        }
    }
}
