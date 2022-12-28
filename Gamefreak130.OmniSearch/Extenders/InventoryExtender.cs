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
                    CurrentInventory = InventoryPanel.Instance.mSecondaryInventory;
                    InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange += OnVisibilityChange;
                    // Reorder callbacks to ensure that ours is fired before the reference to the old inventory is discarded
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged -= InventoryPanel.Instance.OnSecondaryInventoryOwnerChanged;
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged += OnInventoryOwnerChange;
                    HudController.Instance.Model.SecondaryInventoryOwnerChanged += InventoryPanel.Instance.OnSecondaryInventoryOwnerChanged;
                }
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
                    TaskEx.Run(SetSearchModel);
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
                        InventoryGrid.AddItem(new ItemGridCellItem(inventoryItemWin2, inventoryItemStack));
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

        protected override void SetSearchModel() => SetSearchModel(new ExactMatch<IInventoryItemStack>(Corpus));
    }
}
