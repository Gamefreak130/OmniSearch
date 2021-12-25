using Gamefreak130.Common.Tasks;
using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace Gamefreak130.OmniSearchSpace.UI
{
    public class OmniSearchBar : IDisposable
    {
        private readonly Layout mLayout;

        private Window mWindow;

        private TextEdit mInput;

        private uint mTriggerHandle;

        private Action mOnQueryEntered;

        private string mPreviousQuery;

        private const uint kTextInputId = 2;

        public string Query => mInput.Caption;

        public OmniSearchBar(WindowBase parent, Action onQueryEntered)
        {
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }

        public OmniSearchBar(UICategory parent, Action onQueryEntered)
        {
            mLayout = UIManager.LoadLayoutAndAddToWindow(ResourceKey.CreateUILayoutKey("OmniSearchBar", 0U), parent);
            Init(onQueryEntered);
        }

        public OmniSearchBar(WindowBase parent, Action onQueryEntered, float x, float y) : this(parent, onQueryEntered)
        {
            Vector2 offset = new(x, y);
            mWindow.Area = new(mWindow.Area.TopLeft + offset, mWindow.Area.BottomRight + offset);
        }

        public OmniSearchBar(UICategory parent, Action onQueryEntered, float x, float y) : this(parent, onQueryEntered)
        {
            Vector2 offset = new(x, y);
            mWindow.Area = new(mWindow.Area.TopLeft + offset, mWindow.Area.BottomRight + offset);
        }

        // TODO Custom trigger removing Escape
        // TODO Remove/disable triggers and/or their handling when not focused
        private void Init(Action onQueryEntered)
        {
            mOnQueryEntered = onQueryEntered;
            mWindow = mLayout.GetWindowByExportID(1) as Window;
            mTriggerHandle = mWindow.AddTriggerHook("OKCancelDialog", TriggerActivationMode.kPermanent, 17);
            mWindow.TriggerDown += OnTriggerDown;
            mInput = mWindow.GetChildByID(kTextInputId, true) as TextEdit;
        }

        private void OnTriggerDown(WindowBase sender, UITriggerEventArgs eventArgs)
        {
            if (eventArgs.TriggerCode is (uint)ModalDialog.Triggers.kOKTrigger && mInput.Caption != mPreviousQuery)
            {
                TaskEx.Run(() => mOnQueryEntered());
                mPreviousQuery = mInput.Caption;
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
