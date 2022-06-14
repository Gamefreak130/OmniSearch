namespace Gamefreak130.OmniSearchSpace.UI.Extenders
{
    // CONSIDER Hide/show toggle using tab or something
    // CONSIDER Let user choose search model?
    // TODO Blueprint, shopping, playflow, inventory (relationships?) (saves?) (CAS traits/LTWs? Clothes/hair???) extenders
    // TODO Fix shop mode weirdness
    // TEST featured store items
    // TEST resort build/buy
    // TEST interior design
    public abstract class SearchExtender<TDocument, TResult> : IDisposable
    {
        private ISearchModel<TResult> mSearchModel;

        protected virtual ISearchModel<TResult> SearchModel
        {
            get => mSearchModel;
            set
            {
                mSearchModel?.Dispose();
                mSearchModel = value;
                SearchModel?.Preprocess();
            }
        }

        protected OmniSearchBar SearchBar { get; private set; }

        protected abstract IEnumerable<TDocument> Corpus { get; }

        protected SearchExtender(WindowBase parentWindow, string searchBarGroup) 
        {
            EventTracker.AddListener(EventTypeId.kExitInWorldSubState, delegate {
                Dispose();
                return ListenerAction.Remove;
            });

            SearchBar = new(searchBarGroup, parentWindow, QueryEnteredTask);
        }

        public virtual void Dispose()
        {
            SearchBar.Dispose();
            SearchModel = null;
        }

        protected abstract void QueryEnteredTask();
    }

    public abstract class TitleDescriptionSearchExtender<T> : SearchExtender<Document<T>, T>
    {
        protected TitleDescriptionSearchExtender(WindowBase parentWindow, string searchBarGroup) : base(parentWindow, searchBarGroup)
        {
        }

        protected abstract Document<T> SelectDocument(T obj);
    }
}
