using Sims3.Gameplay;

namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    public class MainMenuExtender : DocumentSearchExtender<SaveGameMetadata>
    {
        protected override IEnumerable<SaveGameMetadata> Materials => MainMenu.mSaveGameList;

        private MainMenu MainMenu => ParentWindow as MainMenu;

        public MainMenuExtender() : base(UIManager.mCustomControlInstanceDict.Where(kvp => UIManager.gUIMgr.GetCustomControlID(kvp.Value.WinHandle) == 0xD6DA8D5B)
                                                                             .First()
                                                                             .Value, 
                                         "MainMenu", showFullPanel: false)
        {
            mDocumentCount = MainMenu.mSaveGameList.Count;
            MainMenu.mFsiWorldWindow.VisibilityChange += OnVisibilityChange;
            MainMenu.Tick += OnTick;
        }

        private int mDocumentCount;

        private static Dictionary<string, string> mWorldNameCache;

        public static void Inject()
        {
            MainMenuState mainMenuState = GameStates.sSingleton.mStateMachine.FindStateById(2) as MainMenuState;
            while (mainMenuState.mMainMenuLayout is null)
            {
                if (GameStates.sSingleton.mStateMachine.CurState is ToInWorldState)
                {
                    return;
                }
                TaskEx.Yield();
            }

            new MainMenuExtender();
        }

        public static void CacheWorldNames()
        {
            if (mWorldNameCache is null)
            {
                mWorldNameCache = new();
                foreach (string result in new WorldFileSearch(0))
                {
                    string worldFileName = result.ToLower();
                    if (worldFileName.EndsWith(".world"))
                    {
                        worldFileName = worldFileName.Substring(0, worldFileName.Length - 6);
                    }

                    if (!string.IsNullOrEmpty(worldFileName))
                    {
                        mWorldNameCache[worldFileName] = GetLocalizedWorldName(worldFileName);
                    }
                }
                foreach (string saveFileName in new WorldFileSearch(1))
                {
                    SaveGameMetadata saveGameMetadata = new()
                    {
                        mSaveFile = saveFileName
                    };
                    Responder.Instance.MainMenuModel.GetSaveFileDetails(ref saveGameMetadata);

                    saveGameMetadata.mHomeworldFile = saveGameMetadata.mHomeworldFile.ToLower();
                    saveGameMetadata.mWorldFile = saveGameMetadata.mWorldFile.ToLower();
                    if (!string.IsNullOrEmpty(saveGameMetadata.mHomeworldFile) && !mWorldNameCache.ContainsKey(saveGameMetadata.mHomeworldFile))
                    {
                        mWorldNameCache[saveGameMetadata.mHomeworldFile] = GetLocalizedWorldName(saveGameMetadata.mHomeworldFile);
                    }
                    if (!string.IsNullOrEmpty(saveGameMetadata.mWorldFile) && !mWorldNameCache.ContainsKey(saveGameMetadata.mWorldFile))
                    {
                        mWorldNameCache[saveGameMetadata.mWorldFile] = GetLocalizedWorldName(saveGameMetadata.mWorldFile);
                    }
                }
            }
        }

        private static string GetLocalizedWorldName(string worldFileName)
        {
            WorldFileMetadata worldFileMetadata = new()
            {
                mWorldFile = worldFileName
            };
            Responder.Instance.MainMenuModel.GetWorldFileDetails(ref worldFileMetadata);
            return worldFileMetadata.mCaption;
        }

        private void OnVisibilityChange(WindowBase _, UIVisibilityChangeEventArgs __) => SetSearchBarLocation();

        private void OnTick(WindowBase _, UIEventArgs __)
        {
            if (MainMenu.mSaveGameList.Count != mDocumentCount)
            {
                if (MainMenu.mSaveGameList.Count == 0)
                {
                    Dispose();
                    return;
                }
                SetSearchBarLocation();
                SetSearchModel();
                mDocumentCount = MainMenu.mSaveGameList.Count;
            }
        }

        public override void Dispose()
        {
            MainMenu.mFsiWorldWindow.VisibilityChange -= OnVisibilityChange;
            MainMenu.Tick -= OnTick;
            base.Dispose();
        }

        protected override void ClearItems()
            => MainMenu.mSaveGrid.Clear();

        protected override void ProcessResultsTask(IEnumerable<SaveGameMetadata> results)
        {
            ResourceKey resKey = ResourceKey.CreateUILayoutKey("GameEntryLoadItem", 0U);

            foreach (SaveGameMetadata saveGameMetadata in results)
            {
                Window window = UIManager.LoadLayout(resKey).GetWindowByExportID(1) as Window;
                if (window != null)
                {
                    MainMenu.SetupSaveItem(window, saveGameMetadata);
                    MainMenu.mSaveGrid.AddItem(new ItemGridCellItem(window, saveGameMetadata));
                }
            }

            if (MainMenu.mSaveGrid.Count > 0)
            {
                MainMenu.mSaveGrid.SelectedItem = 0;
                MainMenu.mSaveItem = MainMenu.mSaveGrid.SelectedTag as SaveGameMetadata;
                MainMenu.mbSavedGameMode = true;
                MainMenu.mNewGameButton.Enabled = true;
                MainMenu.mFsiWorldLearnMoreButton.Enabled = true;
                MainMenu.mSkipUserProfileUiElementsRefresh = true;
                MainMenu.RefreshInfoPane();
            }
        }

        protected override Document<SaveGameMetadata> SelectDocument(SaveGameMetadata material)
            => new($"{material.mCaption};{material.mHouseholdName};{(string.IsNullOrEmpty(material.mHomeworldFile) ? "" : mWorldNameCache[material.mHomeworldFile.ToLower()])};{(string.IsNullOrEmpty(material.mWorldFile) ? "" : mWorldNameCache[material.mWorldFile.ToLower()])}",
                   $"{material.mWorldType};{material.mHouseholdBio}", material);

        protected override void SetSearchBarLocation()
        {
            float x, width, y = 730;
            if (MainMenu.mFsiWorldWindow.Visible)
            {
                x = 460;
                width = 175;
            }
            else
            {
                x = 750;
                width = 165;
            }

            SearchBar.SetLocation(x, y, width);
        }

        protected override void SetSearchModel() => SetSearchModel(new TFIDF<SaveGameMetadata>(Corpus));
    }
}