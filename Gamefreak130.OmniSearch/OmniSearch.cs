using Gamefreak130.Common.Tasks;
using Gamefreak130.OmniSearchSpace.UI.Extenders;
using Sims3.Gameplay.EventSystem;
using Sims3.SimIFace;
using Sims3.UI;
using System;

namespace Gamefreak130
{
    public static class OmniSearch
    {
        [Tunable]
        private static readonly bool kCJackB;

        static OmniSearch() => World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;

        // TODO Public API documentation
        private static void OnWorldLoadFinished(object sender, EventArgs e)
            => EventTracker.AddListener(EventTypeId.kEnterInWorldSubState, delegate {
                TaskEx.Run(OnEnterSubState);
                return ListenerAction.Keep;
            });

        private static void OnEnterSubState()
        {
            if (BuyController.sController is not null)
            {
                new BuyExtender();
            }
            if (BuildController.sController is not null)
            {
                //BuildExtender.Init();
            }
        }
    }
}
