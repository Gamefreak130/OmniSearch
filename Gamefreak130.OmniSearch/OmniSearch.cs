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
global using Sims3.UI.GameEntry;
global using Sims3.UI.Hud;
global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq;
using Gamefreak130.Common.UI;
using Gamefreak130.OmniSearchSpace.UI.Extenders;

namespace Gamefreak130
{
    public static class OmniSearch
    {
        [Tunable]
        private static readonly bool kCJackB;

        static OmniSearch()
        {
            World.OnStartupAppEventHandler += OnStartupApp;
            World.OnWorldLoadFinishedEventHandler += OnWorldLoadFinished;
            World.OnEnterNotInWorldEventHandler += (_,_) => TransitionToMainMenu();
        }

        private static void OnStartupApp(object sender, EventArgs e)
        {
            if (CommandLine.FindSwitch("ccinstall") is null && CommandLine.FindSwitch("ccuninstall") is null)
            {
                // GetWorldFileDetails takes an absurdly long time, presumably due to disk access
                // This function does the work and caches it while the initial loading screen is still up
                // To prevent freezing when setting the search model and selecting the documents
                MainMenuExtender.CacheWorldNames();
                TransitionToMainMenu();
            }
        }

        // CONSIDER Play flow sort by in edit town library panel?
        // CONSIDER Smoother build/buy/blueprint, edit town, play flow?
        // CONSIDER Auto-expand Live Mode panels?
        // CONSIDER More robust tokenizer for languages other than English
        // CONSIDER Spelling correction on query typos? autofill incomplete words?
        // TODO Public API documentation
        // TODO Add tunable toggles for individual search extenders
        private static void TransitionToMainMenu()
        {
            // Inject search bar only if there is at least one save game
            if (GameStates.sSingleton.mStateMachine.CurState is not ToMainMenuState && new WorldFileSearch(1).Renumerable<object>().Skip(1).FirstOrDefault() is not null)
            {
                TaskEx.Run(MainMenuExtender.Inject);
            }
        }

        private static void OnWorldLoadFinished(object sender, EventArgs e)
            => EventTracker.AddListener(EventTypeId.kEnterInWorldSubState, delegate {
                TaskEx.Run(OnEnterSubState);
                return ListenerAction.Keep;
            });

        private static void OnEnterSubState()
        {
            if (GameStates.IsLiveState)
            {
                UIManager.sDarkenBackground.VisibilityChange += LiveModeModalInject;
                InventoryPanel.Instance.VisibilityChange += InventoryExtender.InjectIfVisible;
                InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange += InventoryExtender.InjectIfVisible;
                RelationshipsPanel.Instance.VisibilityChange += RelationshipPanelExtender.InjectIfVisible;
            }
            else
            {
                UIManager.sDarkenBackground.VisibilityChange -= LiveModeModalInject;
                InventoryPanel.Instance.VisibilityChange -= InventoryExtender.InjectIfVisible;
                InventoryPanel.Instance.mSecondaryInventoryWin.VisibilityChange -= InventoryExtender.InjectIfVisible;
                RelationshipsPanel.Instance.VisibilityChange -= RelationshipPanelExtender.InjectIfVisible;
                if (BuyController.sController is not null)
                {
                    new BuyExtender();
                }
                else if (BuildController.sController is not null)
                {
                    new BuildExtender();
                }
                else if (EditTownController.Instance is not null)
                {
                    new EditTownExtender();
                }
                else if (BlueprintController.Active)
                {
                    new BlueprintExtender();
                }
                else if (PlayFlowController.Singleton is not null)
                {
                    new PlayFlowExtender();
                }
                else if (ShoppingController.Instance is not null)
                {
                    new ShoppingExtender();
                }
            }
        }

        private static void LiveModeModalInject(WindowBase _, UIVisibilityChangeEventArgs __)
        {
            // CONSIDER Fix double inject on travel and save/reload? Is this necessary?
            if (UIManager.sDarkenBackground.Visible)
            {
                TaskEx.Run(() => {
                    if (UIManager.GetModalWindow() is Dialog dialog && UIHelper.TryGetModalDialog(UIManager.GetModalWindow() as Dialog, out ModalDialog modal))
                    {
                        switch (modal)
                        {
                            case SimplePurchaseDialog simplePurchaseDialog:
                                new SimplePurchaseExtender(simplePurchaseDialog);
                                break;
                            case FestivalTicketDialog festivalTicketDialog:
                                new FestivalDialogExtender(festivalTicketDialog);
                                break;
                            case AdventureRewardsShopDialog adventureRewardsShopDialog:
                                new AdventureShopDialogExtender(adventureRewardsShopDialog);
                                break;
                        }
                    }
                });
            }
        }
    }
}
