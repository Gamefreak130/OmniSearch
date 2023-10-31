global using Gamefreak130.Common.Helpers;
global using Gamefreak130.Common.Loggers;
global using Gamefreak130.Common.Tasks;
global using Gamefreak130.OmniSearchSpace.Helpers;
global using Gamefreak130.OmniSearchSpace.Models;
global using Sims3.Gameplay;
global using Sims3.Gameplay.EventSystem;
global using Sims3.Gameplay.Utilities;
global using Sims3.SimIFace;
global using Sims3.SimIFace.BuildBuy;
global using Sims3.UI;
global using Sims3.UI.CAS;
global using Sims3.UI.GameEntry;
global using Sims3.UI.Hud;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq;
using Gamefreak130.Common.UI;
using Gamefreak130.OmniSearchSpace;
using Gamefreak130.OmniSearchSpace.UI.Extenders;

namespace Gamefreak130
{
    public static class OmniSearch
    {
        [Tunable]
        private static readonly bool kCJackB;

        private static ModalRetriever<ModalDialog> sModalRetriever;

        static OmniSearch()
        {
            World.OnStartupAppEventHandler += OnStartupApp;
            World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;
            World.OnDesignModeStartEventHandler += OnDesignModeStarted;
            World.OnEnterNotInWorldEventHandler += (_,_) => TransitionToMainMenu();
        }

        private static void OnStartupApp(object sender, EventArgs e)
        {
            if (PersistedSettings.kEnableMainMenuSearch && CommandLine.FindSwitch("ccinstall") is null && CommandLine.FindSwitch("ccuninstall") is null)
            {
                // GetWorldFileDetails takes an absurdly long time, presumably due to disk access
                // This function does the work and caches it while the initial loading screen is still up
                // To prevent freezing when setting the search model and selecting the documents
                MainMenuExtender.CacheWorldNames();
                TransitionToMainMenu();
            }
        }

        // OUT OF SCOPE Play flow sort by in edit town library panel?
        // OUT OF SCOPE Smoother build/buy/blueprint, edit town, play flow, CASt; remove filter/select restriction when populating?
        // CONSIDER More robust tokenizer for languages other than English
        // CONSIDER Spelling correction on query typos?
        // TODO Public API documentation
        // TODO Change XML EnableLogging value before release
        // TODO Can we hardcode RefSafetyRulesAttribute, rather than letting the compiler generate it?
        private static void TransitionToMainMenu()
        {
            // Inject search bar only if there is at least one save game
            if (PersistedSettings.kEnableMainMenuSearch && GameStates.sSingleton.mStateMachine.CurState is not ToMainMenuState && new WorldFileSearch(1).Renumerable<object>().Skip(1).FirstOrDefault() is not null)
            {
                TaskEx.Run(MainMenuExtender.Inject);
            }
        }

        private static void OnWorldLoadFinished(object sender, EventArgs e)
        {
            sModalRetriever?.Dispose();
            sModalRetriever = new();
            sModalRetriever.ModalPushed += OnModalPushed;

            EventTracker.AddListener(EventTypeId.kEnterInWorldSubState, delegate {
                TaskEx.Run(OnEnterSubState);
                return ListenerAction.Keep;
            });
        }

        private static void OnEnterSubState()
        {
            if (GameStates.IsLiveState)
            {
                if (PersistedSettings.kEnableInventorySearch)
                {
                    InventoryPanel.Instance.VisibilityChange += InventoryExtender.InjectIfVisible;
                    InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange += InventoryExtender.InjectIfVisible;
                }
                if (PersistedSettings.kEnableRelationshipPanelSearch)
                {
                    RelationshipsPanel.Instance.VisibilityChange += RelationshipPanelExtender.InjectIfVisible;
                }
            }
            else
            {
                InventoryPanel.Instance.VisibilityChange -= InventoryExtender.InjectIfVisible;
                InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange -= InventoryExtender.InjectIfVisible;
                RelationshipsPanel.Instance.VisibilityChange -= RelationshipPanelExtender.InjectIfVisible;
                if (PersistedSettings.kEnableBuySearch && BuyController.sController is not null)
                {
                    new BuyExtender();
                }
                else if (PersistedSettings.kEnableBuildSearch && BuildController.sController is not null)
                {
                    new BuildExtender();
                }
                else if (PersistedSettings.kEnableEditTownSearch && EditTownController.Instance is not null)
                {
                    new EditTownExtender();
                }
                else if (PersistedSettings.kEnableBlueprintSearch && BlueprintController.Active)
                {
                    new BlueprintExtender();
                }
                else if (PersistedSettings.kEnableNewGameSearch && PlayFlowController.Singleton is not null)
                {
                    new PlayFlowExtender();
                }
                else if (PersistedSettings.kEnableShoppingSearch && ShoppingController.Instance is not null)
                {
                    new ShoppingExtender();
                }
            }
        }

        private static void OnModalPushed(ModalDialog modal)
        {
            switch (modal)
            {
                case SimplePurchaseDialog simplePurchaseDialog when PersistedSettings.kEnableSimplePurchaseDialogSearch:
                    new SimplePurchaseDialogExtender(simplePurchaseDialog);
                    break;
                case FestivalTicketDialog festivalTicketDialog when PersistedSettings.kEnableFestivalDialogSearch:
                    new FestivalDialogExtender(festivalTicketDialog);
                    break;
                case AdventureRewardsShopDialog adventureRewardsShopDialog when PersistedSettings.kEnableAdventureShopDialogSearch:
                    new AdventureShopDialogExtender(adventureRewardsShopDialog);
                    break;
                case TraitsPickerDialog traitsPickerDialog when PersistedSettings.kEnableTraitsPickerDialogSearch:
                    new TraitsPickerDialogExtender(traitsPickerDialog);
                    break;
                case WishPickerDialog wishPickerDialog when PersistedSettings.kEnableWishPickerDialogSearch:
                    new WishPickerDialogExtender(wishPickerDialog);
                    break;
            }
        }

        private static void OnDesignModeStarted(object _, EventArgs __)
        {
            if (PersistedSettings.kEnableCASTSearch)
            {
                // Start inject task on EnterFullEditMode, so that the task to initialize the CASCompositorController comes before it
                CASCompositorController.Instance.EnterFullEditMode += CompositorExtender.Inject;
            }
        }
    }
}
