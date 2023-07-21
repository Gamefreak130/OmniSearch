namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // CONSIDER RefreshSearchBarVisibility is only used for BuildBuyExtender; can we make it an abstract method there instead?
    // TODO (AdventureRewardsShopDialog?), (CAS traits/LTWs? CASt???) extenders
    // TEST resort build/buy
    // TEST interior design
    public abstract class SearchExtender<TDocument, TMaterial> : IDisposable
    {
        protected ISearchModel<TMaterial> SearchModel { get; private set; }

        protected OmniSearchBar SearchBar { get; private set; }

        protected WindowBase ParentWindow
        {
            get => mParentWindow;
            set
            {
                if (mParentWindow is not null)
                {
                    mParentWindow.Detach -= OnDetach;
                    SearchBar.Reparent(value);
                }
                mParentWindow = value;
                mParentWindow.Detach += OnDetach;
            }
        }

        protected abstract IEnumerable<TMaterial> Materials { get; }

        protected IEnumerable<TDocument> Corpus => Materials.Select(SelectDocument);

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup, bool visible = true, bool showFullPanel = true)
        {
            ParentWindow = parentWindow;

            SearchBar = new(searchBarGroup, ParentWindow, OnQueryEntered, showFullPanel);
            SetSearchBarVisibility(visible);
        }

        private WindowBase mParentWindow;

        private void OnDetach(WindowBase _, UIEventArgs __) => Dispose();

        public virtual void Dispose()
        {
            ParentWindow.Detach -= OnDetach;
            SearchBar.Dispose();
            SetSearchModel(null);
        }

        protected virtual void SetSearchBarVisibility(bool visible)
        {
            SearchBar.Visible = visible;
            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
                SetSearchModel();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        protected virtual void RefreshSearchBarVisibility()
        {
        }

        protected void ProcessExistingQuery()
        {
            if (!string.IsNullOrEmpty(SearchBar.Query))
            {
                ClearItems();
                SearchBar.TriggerSearch();
            }
            else
            {
                SearchBar.Clear();
            }
        }

        private void OnQueryEntered()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"), UIManager.sDarkenBackground is null || !UIManager.sDarkenBackground.Visible);
#if DEBUG
                IEnumerable<TMaterial> results = SearchModel.Search(SearchBar.Query)
                                                            .ToList();

                //DocumentLogger.sInstance.WriteLog();
#else
                IEnumerable<TResult> results = SearchModel.Search(SearchBar.Query);
#endif

                ClearItems();
                ProcessResultsTask(results);
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
            }
        }

        protected void SetSearchModel(ISearchModel<TMaterial> value)
        {
            try
            {
                SearchModel?.Dispose();
                SearchModel = value;
                SearchModel?.Preprocess();

                if (SearchModel is not null)
                {
                    ProcessExistingQuery();
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected abstract void SetSearchModel();

        protected abstract void SetSearchBarLocation();

        protected abstract void ClearItems();

        protected abstract TDocument SelectDocument(TMaterial material);

        protected abstract void ProcessResultsTask(IEnumerable<TMaterial> results);
    }

    public abstract class DocumentSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected DocumentSearchExtender(WindowBase parentWindow, string searchBarGroup, bool visible = true, bool showFullPanel = true) : base(parentWindow, searchBarGroup, visible, showFullPanel)
        {
        }
    }

    public abstract class ModalExtender<TModalDialog, TMaterial> : DocumentSearchExtender<TMaterial> where TModalDialog : ModalDialog
    {
        protected TModalDialog Modal { get; }

        // Set search bar visibility to false initially, then reset it in this constructor after setting Modal
        // So that we can safely get Materials using Modal if necessary
        public ModalExtender(TModalDialog modal, bool visible = true, bool showFullPanel = false) : base(modal.ModalDialogWindow, "Dialogs", false, showFullPanel)
        {
            Modal = modal;
            SetSearchBarVisibility(visible);
        }
    }
}
