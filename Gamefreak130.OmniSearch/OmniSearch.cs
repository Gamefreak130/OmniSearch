global using Gamefreak130.Common.Helpers;
global using Gamefreak130.Common.Loggers;
global using Gamefreak130.Common.Tasks;
global using Gamefreak130.OmniSearchSpace.Helpers;
global using Gamefreak130.OmniSearchSpace.Models;
global using Sims3.Gameplay.EventSystem;
global using Sims3.Gameplay.Utilities;
global using Sims3.SimIFace;
global using Sims3.UI;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq;
using Gamefreak130.OmniSearchSpace.UI.Extenders;
using Sims3.UI.GameEntry;

namespace Gamefreak130
{
    public static class OmniSearch
    {
        [Tunable]
        private static readonly bool kCJackB;

        static OmniSearch() => World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;

        // CONSIDER Resurrect sort by in edit town library panel?
        // CONSIDER More robust tokenizer for languages other than English
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
                new BuildExtender();
            }
            if (EditTownController.Instance is not null)
            {
                new EditTownExtender();
            }
        }
    }
}
