﻿using Gamefreak130.OmniSearchSpace.Helpers;
using Gamefreak130.OmniSearchSpace.Models;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Utilities;
using Sims3.SimIFace.BuildBuy;
using Sims3.UI.CAS;
using Sims3.UI.Hud;
using Sims3.UI.Store;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Search query history using up key
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
        // TODO cache for collection search?
        protected abstract IEnumerable<TDocument> Corpus { get; }

        protected SearchExtender(WindowBase parentWindow)
        {
            SearchBar = new(parentWindow, QueryEnteredTask);
        }

        public virtual void Dispose()
        {
            SearchBar.Dispose();
            SearchModel = null;
        }

        protected abstract void QueryEnteredTask();
    }

    public abstract class TitleDescriptionSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected TitleDescriptionSearchExtender(WindowBase parentWindow) : base(parentWindow)
        {
        }

        protected abstract Document<T> SelectDocument(T obj);
    }

    public abstract class BuildBuyExtender : TitleDescriptionSearchExtender<object>
    {
        protected const ulong kWallDescriptionHash = 0xDD1EAD49D9F75762;

        protected const ulong kFloorDescriptionHash = 0x2DE87A7A181E89C4;

        private AwaitableTask mPendingQueryTask;

        protected BuildBuyExtender(WindowBase parentWindow) : base(parentWindow)
            => EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                    Dispose();
                    return ListenerAction.Remove;
                });

        public override void Dispose()
        {
            mPendingQueryTask?.Dispose();
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
                SearchModel = new TFIDF<object>(Corpus)
                {
                    Yielding = true
                };

                if (!string.IsNullOrEmpty(SearchBar.Query))
                {
                    ProcessExistingQuery();
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected void ProcessExistingQuery()
        {
            ClearCatalogGrid();
            mPendingQueryTask = TaskEx.Run(QueryEnteredTask);
        }

        protected IEnumerable<object> SearchCollections()
        {
            ITokenizer tokenizer = Tokenizer.Create();
            foreach (IBBCollectionData collection in Responder.Instance.BuildModel.CollectionInfo.CollectionData)
            {
                if (tokenizer.Tokenize(SearchBar.Query).SequenceEqual(tokenizer.Tokenize(collection.CollectionName)))
                {
                    List<Document<object>> collectionDocs = collection.Items.ConvertAll(SelectDocument);
                    IEnumerable<object> collectionProducts = from document in Corpus
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
            // TODO shorthand controller
            mFamilyInventory = BuyController.sController.mFamilyInventory;
            if (mFamilyInventory is not null)
            {
                mFamilyInventory.InventoryChanged += SetSearchModel;
            }

            SearchBar.MoveToBack();
            SetSearchBarLocation();
            bool defaultInventory = BuyController.sController.mCurrCatalogType is BuyController.CatalogType.Inventory;
            if (defaultInventory || BuyController.sController.mPopulateGridTaskHelper is not null)
            {
                if (defaultInventory)
                {
                    RegisterInventoryEvents();
                }
                SetSearchModel();
            }

            CASCompositorController.Instance.ExitFullEditMode += delegate {
                if (!string.IsNullOrEmpty(SearchBar.Query))
                {
                    ProcessExistingQuery();
                }
            };

            BuyController.sController.mTabContainerSortByFunction.TabSelect += OnTabSelect;

            foreach (BuyController.RoomCatalogButtonInfo roomInfo in BuyController.mRoomCatalogButtonInfoDict.Values)
            {
                if (BuyController.sController.GetChildByID(roomInfo.mButtonId, true) is Button button)
                {
                    button.Click += OnRoomSelect;
                }
            }

            uint num = 2;
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

            BuyController.sController.mMiddlePuckWin.VisibilityChange += (_, args) => SearchBar.Visible = args.Visible;
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
                if (BuyController.sController.mCurrCatalogType is not (BuyController.CatalogType.Collections or BuyController.CatalogType.Inventory))
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
                    if (num >= 0 && !string.IsNullOrEmpty(SearchBar.Query))
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
                        if (string.IsNullOrEmpty(SearchBar.Query))
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
                            if (string.IsNullOrEmpty(SearchBar.Query))
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
                    if (string.IsNullOrEmpty(SearchBar.Query))
                    {
                        BuyController.sController.RepopulateInventory();
                    }
                });
            }
        }

        protected override void ClearCatalogGrid()
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
                    // TODO refactor to match buildextender
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
            SetSearchBarLocation();
            SetSearchModel();
        }

        private void OnCatalogButtonClick(WindowBase _, UIButtonClickEventArgs __)
        {
            SearchBar.Clear();
            SetSearchBarLocation();
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
                    if (mCompositorActive && !string.IsNullOrEmpty(SearchBar.Query))
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
            ProcessExistingQuery();
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
