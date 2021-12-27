using Gamefreak130.Common.Tasks;
using Sims3.SimIFace;
using Sims3.UI;
using System;

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

        private readonly Layout mLayout;

        private Window mWindow;

        private TextEdit mInput;

        private uint mTriggerHandle;

        private Action mOnQueryEntered;

        private string mPreviousQuery = "";

        public string Query => mInput.Caption;

        public bool Visible
        {
            get => mWindow.Visible;
            set => mWindow.Visible = value;
        }

        public OmniSearchBar(WindowBase parent, Action onQueryEntered)
        {
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }

        /*public OmniSearchBar(UICategory parent, Action onQueryEntered)
        {
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }*/

        public OmniSearchBar(WindowBase parent, Action onQueryEntered, float x, float y, float width) : this(parent, onQueryEntered) 
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

        /*public OmniSearchBar(UICategory parent, Action onQueryEntered, float x, float y) : this(parent, onQueryEntered)
        {
            Vector2 offset = new(x, y);
            mWindow.Area = new(mWindow.Area.TopLeft + offset, mWindow.Area.BottomRight + offset);
        }*/

        private void Init(Action onQueryEntered)
        {
            mOnQueryEntered = onQueryEntered;
            mWindow = mLayout.GetWindowByExportID(1) as Window;
            mTriggerHandle = mWindow.AddTriggerHook("OKCancelDialog", TriggerActivationMode.kFocus, 17);
            mWindow.TriggerDown += OnTriggerDown;
            mInput = mWindow.GetChildByID((uint)ControlIDs.kTextInput, true) as TextEdit;
        }

        private void OnTriggerDown(WindowBase sender, UITriggerEventArgs eventArgs)
        {
            if (eventArgs.TriggerCode is (uint)ModalDialog.Triggers.kOKTrigger or (uint)ModalDialog.Triggers.kCancelTrigger && mInput.Caption != mPreviousQuery)
            {
                TaskEx.Run(mOnQueryEntered);
                mPreviousQuery = mInput.Caption;
                UIManager.SetFocus(InputContext.kICKeyboard, null);
            }
        }

        public void Dispose()
        {
            mWindow.RemoveTriggerHook(mTriggerHandle);
            //mLayout.Shutdown();
            mLayout.Dispose();
        }
    }
}
