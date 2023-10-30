namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO RewardTraitDialog extender as tutorial example?
    // TEST resort build/buy
    // TEST interior design
    public abstract class SearchExtender<TDocument, TMaterial> : IDisposable
    {
        protected OmniSearchBar SearchBar { get; private set; }

        protected WindowBase ParentWindow
        {
            get => mParentWindow;
            set
            {
                if (mParentWindow is not null)
                {
                    mParentWindow.Detach -= OnDetach;
                    if (value is not null)
                    {
                        SearchBar.Reparent(value);
                    }
                }
                mParentWindow = value;
                if (value is not null)
                {
                    mParentWindow.Detach += OnDetach;
                }
            }
        }

        protected abstract IEnumerable<TMaterial> Materials { get; }

        protected IEnumerable<TDocument> Corpus => Materials.Select(SelectDocument);

        protected virtual bool IsSearchBarVisible => true;

        protected bool Searching => mSearchTask?.IsCompleted ?? false;

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup, bool showFullPanel = true, bool refreshAtStart = true)
        {
            ParentWindow = parentWindow;
            SearchBar = new(searchBarGroup, ParentWindow, OnQueryEntered, showFullPanel);

            if (refreshAtStart)
            {
                RefreshSearchBar();
            }
        }

#if DEBUG
        ~SearchExtender() => SimpleMessageDialog.Show("Finalized extender", GetType().Name);
#endif
        
        private WindowBase mParentWindow;

        private ISearchModel<TMaterial> mSearchModel;

        private AwaitableTask mSearchTask;

        private void OnDetach(WindowBase _, UIEventArgs __) => Dispose();

        public virtual void Dispose()
        {
            ParentWindow = null;
            SearchBar.Dispose();
            SetSearchModel(null);
        }

        protected void SetSearchBarVisibility() => SearchBar.Visible = IsSearchBarVisible;

        protected virtual void RefreshSearchBar()
        {
            SetSearchBarVisibility();
            if (SearchBar.Visible)
            {
                SetSearchBarLocation();
                ResetSearchModel();
            }
            else
            {
                SearchBar.Clear();
            }
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

        private void OnQueryEntered() => SetSearchTask(TaskEx.Run(ProcessQueryTask));

        private void ProcessQueryTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"), UIManager.sDarkenBackground is null || !UIManager.sDarkenBackground.Visible);

                IEnumerable<TMaterial> results = mSearchModel.Search(SearchBar.Query);

                if (PersistedSettings.kEnableLogging)
                {
                    results = results.ToList();
                    DocumentLogger.sInstance.WriteLog(SearchBar.Query);
                }

                ClearItems();
                ProcessResultsTask(results);
            }
            catch (ResetException)
            {
                throw;
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

        private void SetSearchTask(AwaitableTask newTask)
        {
            mSearchTask?.Cancel();
            mSearchTask = newTask;
        }

        protected void ResetSearchModel() => SetSearchModel(GetSearchModel());

        private void SetSearchModel(ISearchModel<TMaterial> value)
        {
            try
            {
                CancelSearch();
                mSearchModel?.Dispose();
                mSearchModel = value;
                mSearchModel?.Preprocess();

                if (mSearchModel is not null)
                {
                    ProcessExistingQuery();
                }
            }
            catch (Exception e)
            {
                ExceptionLogger.sInstance.Log(e);
            }
        }

        protected void CancelSearch() => SetSearchTask(null);

        protected abstract ISearchModel<TMaterial> GetSearchModel();

        protected abstract void SetSearchBarLocation();

        protected abstract void ClearItems();

        protected abstract TDocument SelectDocument(TMaterial material);

        protected abstract void ProcessResultsTask(IEnumerable<TMaterial> results);
    }

    public abstract class DocumentSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected DocumentSearchExtender(WindowBase parentWindow, string searchBarGroup, bool showFullPanel = true, bool refreshAtStart = true) : base(parentWindow, searchBarGroup, showFullPanel, refreshAtStart)
        {
        }
    }

    public abstract class ModalExtender<TModalDialog, TMaterial> : DocumentSearchExtender<TMaterial> where TModalDialog : ModalDialog
    {
        protected TModalDialog Modal { get; }

        public ModalExtender(TModalDialog modal, bool showFullPanel = false) : base(modal.ModalDialogWindow, "Dialogs", showFullPanel, false)
        {
            Modal = modal;
            RefreshSearchBar();
        }
    }
}
