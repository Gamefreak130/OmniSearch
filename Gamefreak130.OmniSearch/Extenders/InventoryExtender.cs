using Sims3.Gameplay.Abstracts;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class InventoryExtender : DocumentSearchExtender<IInventoryItemStack>
    {
        protected override IEnumerable<IInventoryItemStack> Materials => CurrentInventory.InventoryItems;

        protected ItemGrid InventoryGrid 
            => AttachedToPrimaryWindow ? InventoryPanel.Instance.mPrimaryItemGrid : InventoryPanel.Instance.mSecondaryItemGrid;

        protected bool AttachedToPrimaryWindow => ParentWindow == InventoryPanel.Instance.mResizingWindow;

        protected IInventory CurrentInventory
        {
            get => mCurrentInventory ?? (CurrentInventory = AttachedToPrimaryWindow
                                                          ? InventoryPanel.Instance.mCurrentSimInventory
                                                          : InventoryPanel.Instance.mSecondaryInventory);

            set
            {
                if (mCurrentInventory is not null)
                {
                    mCurrentInventory.InventoryChanged -= OnInventoryChange;
                }
                mCurrentInventory = value;
                if (mCurrentInventory is not null)
                {
                    mCurrentInventory.InventoryChanged += OnInventoryChange;
                    OnInventoryChange();
                }
            }
        }

        public InventoryExtender(bool attachToPrimaryWindow)
            : base(InventoryPanel.Instance.GetChildByID(attachToPrimaryWindow ? (uint)InventoryPanel.ControlIDs.ResizingWindow : (uint)InventoryPanel.ControlIDs.SecondaryInventoryWin, true),
                   "Inventory")
        {
            try
            {
                if (AttachedToPrimaryWindow)
                {
                    InventoryPanel.Instance.VisibilityChange += OnVisibilityChange;
                    // Reorder callbacks to ensure that ours is fired before the reference to the old inventory is discarded
                    HudController.Instance.Model.CurrentSimInventoryOwnerChanged -= InventoryPanel.Instance.OnCurrentSimInventoryOwnerChanged;
                    HudController.Instance.Model.CurrentSimInventoryOwnerChanged += OnInventoryOwnerChange;
                    HudController.Instance.Model.CurrentSimInventoryOwnerChanged += InventoryPanel.Instance.OnCurrentSimInventoryOwnerChanged;
                }
                else
                {
                    InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange += OnVisibilityChange;
                    // Reorder callbacks to ensure that ours is fired before the reference to the old inventory is discarded
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged -= InventoryPanel.Instance.OnSecondaryInventoryOwnerChanged;
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged += OnInventoryOwnerChange;
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged += InventoryPanel.Instance.OnSecondaryInventoryOwnerChanged;
                }
                InventoryGrid.InternalGrid.DragEnd -= InventoryPanel.Instance.OnItemGridDragEnd;
                InventoryGrid.InternalGrid.DragEnd += OnItemGridDragEnd;
            }
            catch (Exception ex)
            {
                ExceptionLogger.sInstance.Log(ex);
            }
        }

        private IInventory mCurrentInventory;

        public static void InjectIfVisible(WindowBase sender, UIVisibilityChangeEventArgs args)
        {
            if (args.Visible)
            {
                new InventoryExtender(sender == InventoryPanel.Instance);
            }
        }

        private void OnVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __) => Dispose(false);

        private void OnInventoryChange()
        {
            try
            {
                if (InventoryPanel.Instance is not null)
                {
                    TaskEx.Run(ResetSearchModel);
                }
            }
            catch (Exception ex)
            {
                ExceptionLogger.sInstance.Log(ex);
            }
        }

        private void OnInventoryOwnerChange(IInventory newInventory)
        {
            try
            {
                CurrentInventory = newInventory;
            }
            catch (Exception ex)
            {
                ExceptionLogger.sInstance.Log(ex);
            }
        }

        public override void Dispose() => Dispose(true);

        protected void Dispose(bool detached)
        {
            try
            {
                if (!detached)
                {
                    if (AttachedToPrimaryWindow)
                    {
                        InventoryPanel.Instance.VisibilityChange -= OnVisibilityChange;
                    }
                    else
                    {
                        ParentWindow.VisibilityChange -= OnVisibilityChange;
                    }
                    InventoryGrid.InternalGrid.DragEnd -= OnItemGridDragEnd;
                    // Clear search results if cameraman mode activated
                    InventoryPanel.Instance.RepopulateInventory(CurrentInventory);
                }

                CurrentInventory = null;
                HudController.Instance.Model.CurrentSimInventoryOwnerChanged -= OnInventoryOwnerChange;
                HudController.Instance.Model.SecondaryInventoryOwnerChanged -= OnInventoryOwnerChange;
                base.Dispose();
            }
            catch (Exception ex) 
            { 
                ExceptionLogger.sInstance.Log(ex);
            }
        }

        protected override void ClearItems() => InventoryGrid.Clear();

        protected override void ProcessResultsTask(IEnumerable<IInventoryItemStack> results)
        {
            foreach (IInventoryItemStack inventoryItemStack in results)
            {
                int count = inventoryItemStack.Count;
                if (count > 0)
                {
                    ObjectGuid topObject = inventoryItemStack.TopObject;
                    if (topObject != ObjectGuid.InvalidObjectGuid)
                    {
                        InventoryItemWin inventoryItemWin2 = InventoryItemWin.MakeEmptySlot();
                        inventoryItemWin2.Thumbnail = topObject;
                        inventoryItemWin2.StackItemCount = count;
                        inventoryItemWin2.InUse = inventoryItemStack.InUse;
                        inventoryItemWin2.ObjectToCheckForInteractions = topObject;
                        inventoryItemWin2.DisposeOnDetach = !AttachedToPrimaryWindow;
                        inventoryItemWin2.Shareable = Responder.Instance.HudModel.GetIsShareableFromObjectGuid(topObject);
                        if (inventoryItemWin2.StackItemCount > 1)
                        {
                            InventoryPanel.Instance.AttachGrabAllHandleEvents(inventoryItemWin2);
                        }
                        inventoryItemWin2.Tag = inventoryItemStack;
                        InventoryGrid.AddItem(new(inventoryItemWin2, inventoryItemStack));
                    }
                }
            }
        }

        protected override Document<IInventoryItemStack> SelectDocument(IInventoryItemStack material) 
            => new(GameObject.GetObject(material.TopObject).ToTooltipString(), "", material);

        protected override void SetSearchBarLocation()
        {
            float x, y, width;
            if (AttachedToPrimaryWindow)
            {
                x = -193;
                y = -63;
                width = 170;
            }
            else
            {
                x = 200;
                y = -35;
                width = 175;
            }
            SearchBar.SetLocation(x, y, width);
            SearchBar.MoveToBack();
        }

        protected override ISearchModel<IInventoryItemStack> GetSearchModel() => new ExactMatch<IInventoryItemStack>(Corpus);

        private void OnItemGridDragEnd(WindowBase sender, UIDragEventArgs eventArgs)
        {
            if (eventArgs.Data is not LiveDragData)
            {
                return;
            }

            InventoryPanel.Instance.mbDragging = false;
            IInventory inventory = (sender == InventoryPanel.Instance.mPrimaryItemGrid.InternalGrid) ? InventoryPanel.Instance.mCurrentSimInventory : InventoryPanel.Instance.mSecondaryInventory;
            ItemGrid itemGrid = (sender == InventoryPanel.Instance.mPrimaryItemGrid.InternalGrid) ? InventoryPanel.Instance.mPrimaryItemGrid : InventoryPanel.Instance.mSecondaryItemGrid;
            int num = -1;
            bool flag = true;
            if (eventArgs.Result)
            {
                InventoryPanel.Instance.UnregisterInventoryEvents(inventory);
                InventoryPanel.Instance.SetInventoryInUseForLiveDraggedObjects(false);
                if (InventoryPanel.Instance.mDragInfo is not null)
                {
                    if (InventoryPanel.Instance.mDragInfo.mDragOriginStack is not null && InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory is not null)
                    {
                        int num2 = InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory.InventoryItems.IndexOf(InventoryPanel.Instance.mDragInfo.mDragOriginStack);
                        int count = InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory.InventoryItems.Count;
                        if (InventoryPanel.Instance.mDragInfo.mbDragOriginIsStack && InventoryPanel.Instance.mDragInfo.mDragDestinationInventory is not null)
                        {
                            if ((eventArgs.Modifiers & Modifiers.kModifierMaskShift) is Modifiers.kModifierMaskShift)
                            {
                                InventoryPanel.Instance.mLiveDragHelper.ShiftedDrop = true;
                                World.HandToolDetach();
                                Simulator.AddObject(new OneShotFunctionWithParams(InventoryPanel.Instance.ShiftDragHelper, sender));
                                return;
                            }

                            int num3 = InventoryPanel.Instance.mDragInfo.mDragDestinationInventory.MaxInsertNewStackAt(InventoryPanel.Instance.mLiveDragHelper.DraggedObjects, InventoryPanel.Instance.mDragInfo.mDragDestinationIndex, InventoryPanel.Instance.mInsertionType is InventoryItemWin.InsertionType.Stack);
                            if (num3 > 0 && num3 < InventoryPanel.Instance.mDragInfo.mDragOriginStack.Count)
                            {
                                InventoryPanel.Instance.DoPartialDrop(num3, inventory, itemGrid);
                                return;
                            }

                            flag &= InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory.TryRemoveStackFromInventory(InventoryPanel.Instance.mDragInfo.mDragOriginStack);
                        }
                        else
                        {
                            flag &= InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory.TryRemoveObjectFromInventory(InventoryPanel.Instance.mDragInfo.mDragOriginTopObject);
                            if (flag && InventoryPanel.Instance.mLiveDragHelper.DraggedObjects.Count > 0 && InventoryPanel.Instance.mDragInfo.mDragDestinationInventory is null)
                            {
                                InventoryPanel.Instance.mDragInfo.mDragOriginTopObject = InventoryPanel.Instance.mLiveDragHelper.DraggedObjects[0];
                            }
                        }

                        if (flag && InventoryPanel.Instance.mDragInfo.mDragDestinationInventory == InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory && InventoryPanel.Instance.mDragInfo.mbDragDestinationInsert && num2 < InventoryPanel.Instance.mDragInfo.mDragDestinationIndex)
                        {
                            InventoryPanel.Instance.mDragInfo.mDragDestinationIndex -= count - InventoryPanel.Instance.mDragInfo.mDragOriginatingInventory.InventoryItems.Count;
                        }
                    }

                    if (InventoryPanel.Instance.mDragInfo.mDragDestinationInventory is not null)
                    {
                        if (InventoryPanel.Instance.mDragInfo.mbDragDestinationInsert)
                        {
                            flag &= InventoryPanel.Instance.mDragInfo.mDragDestinationInventory.TryInsertNewStackAt(InventoryPanel.Instance.mLiveDragHelper.DraggedObjects, InventoryPanel.Instance.mDragInfo.mDragDestinationIndex, InventoryPanel.Instance.mInsertionType is InventoryItemWin.InsertionType.Stack);
                            InventoryPanel.Instance.mLiveDragHelper.PurgeHandToolObjects();
                            if (flag)
                            {
                                Audio.StartSound("ui_object_plop");
                            }
                        }
                        else
                        {
                            flag &= InventoryPanel.Instance.mLiveDragHelper.TryAddHandToolObjectsToInventory(InventoryPanel.Instance.mDragInfo.mDragDestinationInventory);
                        }
                    }
                }

                InventoryPanel.Instance.RegisterInventoryEvents(inventory);
                if (flag)
                {
                    if (InventoryPanel.Instance.mDragInfo is not null && InventoryPanel.Instance.mDragInfo.mDragOriginWin is not null)
                    {
                        num = itemGrid.Items.FindIndex((ItemGridCellItem item) => item.mWin == InventoryPanel.Instance.mDragInfo.mDragOriginWin);
                    }

                    InventoryPanel.Instance.RepopulateInventory(inventory);
                }
            }
            else
            {
                InventoryPanel.Instance.UnregisterInventoryEvents(inventory);
                InventoryPanel.Instance.SetInventoryInUseForLiveDraggedObjects(false);
                InventoryPanel.Instance.RegisterInventoryEvents(inventory);
                InventoryPanel.Instance.mLiveDragHelper.PurgeHandToolObjects();
                if (InventoryPanel.Instance.mDragInfo is not null && InventoryPanel.Instance.mDragInfo.mDragOriginWin is not null)
                {
                    num = itemGrid.Items.FindIndex((ItemGridCellItem item) => item.mWin == InventoryPanel.Instance.mDragInfo.mDragOriginWin);
                    if (InventoryPanel.Instance.mDragInfo.mDragOriginStack.Count > 0)
                    {
                        WindowBase parent = InventoryPanel.Instance.mDragInfo.mDragOriginWin.Parent;
                        if (parent is not null)
                        {
                            ZoopBack(InventoryPanel.Instance.mDragInfo.mDragOriginStack.TopObject, sender.WindowToScreen(eventArgs.MousePosition), parent.WindowToScreen(InventoryPanel.Instance.mDragInfo.mDragOriginWin.Position), inventory);
                        }
                        else
                        {
                            ZoopBack(InventoryPanel.Instance.mDragInfo.mDragOriginStack.TopObject, sender.WindowToScreen(eventArgs.MousePosition), InventoryPanel.Instance.mDragInfo.mDragOriginPosition, inventory);
                        }
                    }
                }
            }

            if (InventoryPanel.Instance.mLiveDragHelper.DraggedObjectsCount == 0)
            {
                InventoryPanel.Instance.mDragInfo = null;
            }
            else if (InventoryPanel.Instance.mDragInfo is not null && InventoryPanel.Instance.mDragInfo.mDragOriginWin is not null && num != -1)
            {
                InventoryItemWin inventoryItemWin = itemGrid.Items[num].mWin as InventoryItemWin;
                if (inventoryItemWin is not null)
                {
                    InventoryPanel.Instance.mDragInfo.mDragOriginWin = inventoryItemWin;
                    InventoryPanel.Instance.mDragInfo.mDragOriginWin.ItemDrawState = 1u;
                    InventoryPanel.Instance.mDragInfo.mDragOriginWin.GrabAllHandleWin.DrawState = 1u;
                    InventoryPanel.Instance.mDragInfo.mDragOriginWin.PreviewStackItemCount = InventoryPanel.Instance.mDragInfo.mDragOriginStack.Count - InventoryPanel.Instance.mLiveDragHelper.DraggedObjectsCount;
                }
            }
        }

        private void ZoopBack(ObjectGuid topObject, Vector2 startPosition, Vector2 endPosition, IInventory inventory)
        {
            if (InventoryPanel.Instance.mZoopWindow is null)
            {
                Layout layout = UIManager.LoadLayout(ResourceKey.CreateUILayoutKey("HudInventoryItemWin", 0u));
                InventoryPanel.Instance.mZoopWindow = layout.GetWindowByExportID(1) as InventoryItemWin;
                InventoryPanel.Instance.mZoopWindow.BackgroundWin.Visible = false;
                InventoryPanel.Instance.mZoopWindow.Thumbnail = topObject;
                InventoryPanel.Instance.mZoopWindow.Position = startPosition;
                GlideEffect glideEffect = new()
                {
                    TriggerType = EffectBase.TriggerTypes.Manual,
                    Duration = 0.2f,
                    InterpolationType = EffectBase.InterpolationTypes.EaseInOut,
                    EaseTimes = new(0.2f, 0f),
                    Offset = endPosition - startPosition
                };
                InventoryPanel.Instance.mZoopWindow.EffectList.Add(glideEffect);
                UIManager.GetUITopWindow().AddChild(InventoryPanel.Instance.mZoopWindow);
                glideEffect.TriggerEffect(false);
                TaskEx.Delay(200).ContinueWith(delegate {
                    if (InventoryPanel.Instance.mZoopWindow is not null)
                    {
                        InventoryPanel.Instance.mZoopWindow.Parent.DestroyChild(InventoryPanel.Instance.mZoopWindow);
                        InventoryPanel.Instance.mZoopWindow = null;
                    }
                    ResetSearchModel();
                    if (string.IsNullOrEmpty(SearchBar.Query))
                    {
                        InventoryPanel.Instance.RepopulateInventory(inventory);
                    }
                });
            }
        }
    }
}
