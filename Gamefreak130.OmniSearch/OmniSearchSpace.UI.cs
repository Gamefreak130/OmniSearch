namespace Gamefreak130.OmniSearchSpace.UI
{
    public class OmniSearchBar : IDisposable
    {
        private enum ControlIDs : byte
        {
            kTextInput = 2,
            kTextInputBackground = 6,
            kBackgroundWindow = 8
        }

        private static readonly Dictionary<string, List<string>> sGroupQueryHistory = new();

        private readonly Layout mLayout;

        private WindowBase mWindow;

        private TextEdit mInput;

        private uint mTriggerHandle;

        private Action mOnQueryEntered;

        private readonly string mGroup;

        private bool mQueryEntered;

        private int mHistoryIndex;

        private AwaitableTask mPendingQueryTask;

        private string IndexedHistoryQuery 
            => sGroupQueryHistory[mGroup].Count == 0 || mHistoryIndex == 0 ? "" : sGroupQueryHistory[mGroup][^mHistoryIndex];

        private string PreviousQuery
        {
            get => mQueryEntered ? sGroupQueryHistory[mGroup].LastOrDefault(string.Empty) : "";
            set
            {
                mQueryEntered = !string.IsNullOrEmpty(value);
                if (PreviousQuery != value)
                {
                    sGroupQueryHistory[mGroup].Add(value);
                }
                mHistoryIndex = 1;
            }
        }

        public string Query => mInput.Caption;

        public bool Visible
        {
            get => mWindow.Visible;
            set => mWindow.Visible = value;
        }

        public OmniSearchBar(string group, WindowBase parent, Action onQueryEntered)
        {
            mGroup = group;
            if (!sGroupQueryHistory.ContainsKey(mGroup))
            {
                sGroupQueryHistory.Add(mGroup, new());
            }
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }

        public OmniSearchBar(string group, WindowBase parent, Action onQueryEntered, float x, float y, float width) : this(group, parent, onQueryEntered) 
            => SetLocation(x, y, width);

        public void SetLocation(float x, float y, float width)
        {
            Vector2 offset = new(x, y);
            Vector2 widthVec = new(width, mWindow.Area.BottomRight.y - mWindow.Area.TopLeft.y);
            mWindow.Area = new(offset, widthVec + offset);

            WindowBase background = mWindow.GetChildByID((uint)ControlIDs.kTextInputBackground, true);
            widthVec = new(background.Area.TopLeft.x + width - 10, background.Area.BottomRight.y);
            background.Area = new(background.Area.TopLeft, widthVec);

            widthVec = new(widthVec.x - 10, mInput.Area.BottomRight.y);
            mInput.Area = new(mInput.Area.TopLeft, widthVec);
        }

        public void MoveToBack() => mWindow.MoveToBack();

        public void MoveToFront() => mWindow.MoveToFront();

        public void Clear()
        {
            mInput.Caption = "";
            mQueryEntered = false;
            mHistoryIndex = 0;
        }

        public void TriggerSearch()
        {
            mPendingQueryTask = TaskEx.Run(mOnQueryEntered);
            PreviousQuery = mInput.Caption;
        }

        private void Init(Action onQueryEntered)
        {
            mOnQueryEntered = onQueryEntered;
            mWindow = mLayout.GetWindowByExportID(1) as Window;
            mTriggerHandle = mWindow.AddTriggerHook("OmniSearchBar", TriggerActivationMode.kManual, 17);
            mWindow.TriggerDown += OnTriggerDown;
            mInput = mWindow.GetChildByID((uint)ControlIDs.kTextInput, true) as TextEdit;
            mInput.FocusAcquired += OnFocusAcquired;
            mInput.FocusLost += OnFocusLost;
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
                    mHistoryIndex = Math.Min(mHistoryIndex + 1, sGroupQueryHistory[mGroup].Count);
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
            mPendingQueryTask?.Dispose();
            mWindow.RemoveTriggerHook(mTriggerHandle);
            mLayout.Dispose();
        }
    }
}
