namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO Shopping, playflow, inventory (relationships?) (saves?) (CAS traits/LTWs? Clothes/hair???) extenders
    // TODO Fix shop mode weirdness
    // TEST featured store items
    // TEST resort build/buy
    // TEST interior design
    public abstract class SearchExtender<TDocument, TResult> : IDisposable
    {
        protected ISearchModel<TResult> SearchModel { get; private set; }

        protected OmniSearchBar SearchBar { get; private set; }

        protected abstract IEnumerable<TDocument> Corpus { get; }

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup)
        {
            EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                Dispose();
                return ListenerAction.Remove;
            });

            SearchBar = new(searchBarGroup, parentWindow, OnQueryEntered);
            SetSearchBarVisibility();
        }

        public virtual void Dispose()
        {
            SearchBar.Dispose();
            SetSearchModel(null);
        }

        protected void SetSearchModel(ISearchModel<TResult> value)
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

        protected virtual void SetSearchModel() 
            => throw new NotSupportedException($"No SetSearchModel() override in {GetType().Name}. Provide an override to the parameterless method or pass an ISearchModel as an argument.");

        protected virtual void SetSearchBarVisibility()
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
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));
#if DEBUG
                IEnumerable<TResult> results = SearchModel.Search(SearchBar.Query)
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

        protected abstract void ClearItems();

        protected abstract void ProcessResultsTask(IEnumerable<TResult> results);
    }

    public abstract class DocumentSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected DocumentSearchExtender(WindowBase parentWindow, string searchBarGroup) : base(parentWindow, searchBarGroup)
        {
        }

        protected abstract Document<T> SelectDocument(T obj);
    }
}
