﻿namespace Gamefreak130.OmniSearchSpace.UI
{
    public class OmniSearchBar : IDisposable
    {
        private enum ControlIDs : uint
        {
            kTextInput = 0x424E996A,             // Gamefreak130_OmniSearchBarTextInput
            kTextInputBackground = 0xFC97BFB2,   // Gamefreak130_OmniSearchBarTextInputBackground
            kSearchIcon = 0x8D35DC00,            // Gamefreak130_OmniSearchBarSearchIcon 
            kCollapseButton = 0x4C60C9DE         // Gamefreak130_OmniSearchBarCollapseButton
        }

        private static readonly Dictionary<string, SearchGroup> sSearchGroups = new();

        private readonly Layout mLayout;

        private readonly bool mShowFullPanel;

        private Window mWindow;

        private TextEdit mInput;

        private WindowBase mInputBackground;

        private WindowBase mSearchIcon;

        private Button mCollapseButton; 

        private uint mTriggerHandle;

        private float mExpandedWidth;

        private Action mOnQueryEntered;

        private readonly string mGroup;

        private bool mQueryEntered;

        private int mHistoryIndex;

        private string IndexedHistoryQuery 
            => sSearchGroups[mGroup].QueryHistory.Count == 0 || mHistoryIndex == 0 ? "" : sSearchGroups[mGroup].QueryHistory[^mHistoryIndex];

        private string PreviousQuery
        {
            get => mQueryEntered ? sSearchGroups[mGroup].QueryHistory.LastOrDefault(string.Empty) : "";
            set
            {
                mQueryEntered = !string.IsNullOrEmpty(value);
                if (PreviousQuery != value)
                {
                    sSearchGroups[mGroup].QueryHistory.Add(value);
                }
                mHistoryIndex = mQueryEntered ? 1 : 0;
            }
        }

        public string Query => mInput.Caption;

        public bool Visible
        {
            get => mWindow.Visible;
            set => mWindow.Visible = value;
        }

        public OmniSearchBar(string group, WindowBase parent, Action onQueryEntered, bool showFullPanel = true)
        {
            mShowFullPanel = showFullPanel;
            mGroup = group;
            if (!sSearchGroups.ContainsKey(mGroup))
            {
                sSearchGroups[mGroup] = new();
            }
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }

        public OmniSearchBar(string group, WindowBase parent, Action onQueryEntered, float x, float y, float width, bool showFullPanel = true) : this(group, parent, onQueryEntered, showFullPanel) 
            => SetLocation(x, y, width);

#if DEBUG
        ~OmniSearchBar()
        {
            SimpleMessageDialog.Show("Finalized searchbar", mOnQueryEntered.Target.GetType().Name);
        }
#endif

        public void SetLocation(float x, float y, float width)
        {
            Vector2 offset = new(x, y);
            Vector2 widthVec;
            mExpandedWidth = width;
            if (mShowFullPanel && sSearchGroups[mGroup].Collapsed)
            {
                mCollapseButton.Selected = mInput.Visible
                                         = mInputBackground.Visible
                                         = mSearchIcon.Visible
                                         = false;

                widthVec = new(26, mWindow.Area.BottomRight.y - mWindow.Area.TopLeft.y);
                mWindow.Area = new(offset, widthVec + offset);
            }
            else
            {
                mCollapseButton.Selected = mInput.Visible
                                         = mInputBackground.Visible
                                         = mSearchIcon.Visible
                                         = true;

                widthVec = new(width, mWindow.Area.BottomRight.y - mWindow.Area.TopLeft.y);
                mWindow.Area = new(offset, widthVec + offset);

                widthVec = new(mInputBackground.Area.TopLeft.x + width - 25, mInputBackground.Area.BottomRight.y);
                mInputBackground.Area = new(mInputBackground.Area.TopLeft, widthVec);

                widthVec = new(widthVec.x - 10, mInput.Area.BottomRight.y);
                mInput.Area = new(mInput.Area.TopLeft, widthVec);
            }
        }

        public void MoveToBack() => mWindow.MoveToBack();

        public void MoveToFront() => mWindow.MoveToFront();

        public void Reparent(WindowBase newParent)
        {
            mWindow.RemoveTriggerHook(mTriggerHandle);
            UIManager.Reparent(mWindow, newParent, true);
            mTriggerHandle = mWindow.AddTriggerHook("OmniSearchBar", TriggerActivationMode.kManual, int.MaxValue);
        }

        public void Clear()
        {
            mInput.Caption = "";
            mQueryEntered = false;
            mHistoryIndex = 0;
        }

        public void TriggerSearch()
        {
            mOnQueryEntered();
            PreviousQuery = mInput.Caption;
        }

        private void Init(Action onQueryEntered)
        {
            mOnQueryEntered = onQueryEntered;
            mWindow = mLayout.GetWindowByExportID(1) as Window;
            mTriggerHandle = mWindow.AddTriggerHook("OmniSearchBar", TriggerActivationMode.kManual, int.MaxValue);
            mWindow.TriggerDown += OnTriggerDown;
            mInput = mWindow.GetChildByID((uint)ControlIDs.kTextInput, true) as TextEdit;
            mInput.FocusAcquired += OnFocusAcquired;
            mInput.FocusLost += OnFocusLost;
            mInputBackground = mWindow.GetChildByID((uint)ControlIDs.kTextInputBackground, true);
            mSearchIcon = mWindow.GetChildByID((uint)ControlIDs.kSearchIcon, true);
            mCollapseButton = mWindow.GetChildByID((uint)ControlIDs.kCollapseButton, true) as Button;
            mCollapseButton.Click += OnToggleCollapsed;
            if (!mShowFullPanel)
            {
                mCollapseButton.Visible = false;
                (mWindow.Drawable as StdDrawable)[DrawableBase.ControlStates.kNormal] = null;
            }
        }

        private void OnToggleCollapsed(WindowBase _, UIButtonClickEventArgs __)
        {
            Audio.StartSound("ui_wall_view_panel_open");
            sSearchGroups[mGroup].Collapsed = !sSearchGroups[mGroup].Collapsed;
            SetLocation(mWindow.Position.x, mWindow.Position.y, mExpandedWidth);
        }

        private void OnFocusAcquired(WindowBase _, UIFocusChangeEventArgs eventArgs)
        {
            if (eventArgs.InputContext is InputContext.kICKeyboard)
            {
                UIManager.ActivateTriggerHook(mTriggerHandle);
            }
        }

        private void OnFocusLost(WindowBase _, UIFocusChangeEventArgs eventArgs)
        {
            if (eventArgs.InputContext is InputContext.kICKeyboard)
            {
                UIManager.DeactivateTriggerHook(mTriggerHandle);
            }
        }

        private void OnTriggerDown(WindowBase sender, UITriggerEventArgs eventArgs)
        {
            switch (eventArgs.TriggerCode)
            {
                case (uint)ModalDialog.Triggers.kOKTrigger or (uint)ModalDialog.Triggers.kCancelTrigger when mInput.Caption != PreviousQuery:
                    TriggerSearch();
                    goto case (uint)ModalDialog.Triggers.kOKTrigger;
                case (uint)ModalDialog.Triggers.kOKTrigger:
                case (uint)ModalDialog.Triggers.kCancelTrigger:
                    UIManager.SetFocus(InputContext.kICKeyboard, UIManager.GetSceneWindow());
                    break;
                case (uint)ModalDialog.Triggers.kBackwardTrigger:
                    mHistoryIndex = Math.Min(mHistoryIndex + 1, sSearchGroups[mGroup].QueryHistory.Count);
                    mInput.Caption = IndexedHistoryQuery;
                    break;
                case (uint)ModalDialog.Triggers.kForwardTrigger:
                    mHistoryIndex = Math.Max(mHistoryIndex - 1, 0);
                    mInput.Caption = IndexedHistoryQuery;
                    break;
            }
        }

        public void Dispose()
        {
            mWindow.RemoveTriggerHook(mTriggerHandle);
            mLayout.Shutdown();
            mLayout.Dispose();
        }
    }
}
