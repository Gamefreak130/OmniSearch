namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO RefreshSearchBarVisibility is only used for BuildBuyExtender; can we make it an abstract method there instead?
    // TODO RewardTraitDialog extender as tutorial example?
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

        protected bool Searching => mSearchTask?.IsCompleted ?? false;

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup, bool visible = true, bool showFullPanel = true)
        {
            ParentWindow = parentWindow;

            SearchBar = new(searchBarGroup, ParentWindow, OnQueryEntered, showFullPanel);
            SetSearchBarVisibility(visible);
        }

#if DEBUG
        ~SearchExtender() => SimpleMessageDialog.Show("Finalized extender", GetType().Name);
#endif

        private WindowBase mParentWindow;

        private AwaitableTask mSearchTask;

        private void OnDetach(WindowBase _, UIEventArgs __) => Dispose();

        public virtual void Dispose()
        {
            ParentWindow = null;
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

        private void OnQueryEntered() => SetSearchTask(TaskEx.Run(ProcessQueryTask));

        private void ProcessQueryTask()
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"), UIManager.sDarkenBackground is null || !UIManager.sDarkenBackground.Visible);
//#if DEBUG
                IEnumerable<TMaterial> results = SearchModel.Search(SearchBar.Query)
                                                            .ToList();

                DocumentLogger.sInstance.WriteLog(SearchBar.Query);
//#else
//              IEnumerable<TMaterial> results = SearchModel.Search(SearchBar.Query);
//#endif

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

        protected void SetSearchModel(ISearchModel<TMaterial> value)
        {
            try
            {
                CancelSearch();
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

        protected void CancelSearch() => SetSearchTask(null);

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
