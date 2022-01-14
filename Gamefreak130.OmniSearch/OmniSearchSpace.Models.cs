using Gamefreak130.OmniSearchSpace.Helpers;
using Sims3.Gameplay.Utilities;
using System.Diagnostics;

namespace Gamefreak130.OmniSearchSpace.Models
{
    public interface ISearchModel : IDisposable
    {
        bool Yielding { get; set; }

        void Preprocess();

        IEnumerable Search(string query);
    }

    public interface ISearchModel<T> : ISearchModel
    {
        new IEnumerable<T> Search(string query);
    }

    public abstract class SearchModel<T> : ISearchModel<T>
    {
        protected IEnumerable<Document<T>> mDocuments;

        private AwaitableTask mModelPreprocessTask;

        private AwaitableTask ModelPreprocessTask
        {
            get => mModelPreprocessTask;
            set
            {
                mModelPreprocessTask?.Cancel();
                mModelPreprocessTask = value;
            }
        }

        public bool Yielding { get; set; }

        public SearchModel(IEnumerable<Document<T>> documents)
            => mDocuments = documents;

        ~SearchModel() => Dispose(true);

        public void Dispose() => Dispose(false);

        private void Dispose(bool finalizing)
        {
            ModelPreprocessTask = null;
            if (!finalizing)
            {
                GC.SuppressFinalize(this);
            }
        }

        protected bool ShouldYield(StopWatch startTimer)
            => Yielding && startTimer.GetElapsedTimeFloat() >= 1000f / PersistedSettings.kPreprocessingTickRate;

        public void Preprocess()
            => ModelPreprocessTask = TaskEx.Run(PreprocessTask);

        protected virtual void PreprocessTask()
        {
        }

        public IEnumerable<T> Search(string query)
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"));

                if (string.IsNullOrEmpty(query))
                {
                    return from document in mDocuments
                           select LogWeight(document, float.NaN);
                }

                TaskEx.Delay((uint)(ProgressDialog.kProgressDialogDelay * 1000))
                      .ContinueWith(_ => Yielding = false);

                ModelPreprocessTask.Await();
                return SearchTask(query);
            }
            finally
            {
                TaskEx.Run(ProgressDialog.Close);
            }
        }

        IEnumerable ISearchModel.Search(string query) => Search(query);

        protected abstract IEnumerable<T> SearchTask(string query);

        protected T LogWeight(Document<T> document, float weight)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Log(0, "", $"{document.Title}\n{document.Description}\n{weight}\n\n");
            }
            else
            {
                // TODO Log to scripterror
            }
            return document.Tag;
        }
    }

    public class ExactMatch<T> : SearchModel<T>
    {
        public ExactMatch(IEnumerable<Document<T>> documents) : base(documents)
        {
        }

        protected override IEnumerable<T> SearchTask(string query) 
            => from document in mDocuments
               // Little trick I learned from StackOverflow to efficiently count substring occurrences
               // https://stackoverflow.com/questions/541954/how-would-you-count-occurrences-of-a-string-actually-a-char-within-a-string
               let titleWeight = (float)(document.Title.Length - document.Title.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kTitleWeight
               let descWeight = (float)(document.Description.Length - document.Description.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kDescriptionWeight
               let weight = titleWeight + descWeight
               where weight > 0
               orderby weight descending
               select LogWeight(document, weight);
    }

    public class TFIDF<T> : SearchModel<T>
    {
        private new readonly List<Document<T>> mDocuments;

        // TFIDFMatrix is a set of document vector embeddings formed from the term frequency of words in each document weighted using TF-IDF
        // The embedding itself is represented as a word dictionary, since the full vector of all words in the corpus would be extremely sparse
        private readonly List<Dictionary<string, double>> mTfidfMatrix;

        // WordOccurences is a mapping of words to the documents containing them, allowing us to easily calculate IDF
        // Using a HashSet of document indices rather than a document frequency count makes it easier to avoid double counting words appearing multiple times in a single document
        private readonly Dictionary<string, HashSet<int>> mWordOccurences;

        // ChampionLists is a set of the "top documents" in the corpus with the highest frequencies of a given word
        private readonly Dictionary<string, List<int>> mChampionLists;

        private readonly ITokenizer mTokenizer;

        public TFIDF(IEnumerable<Document<T>> documents) : base(documents)
        {
            mDocuments = base.mDocuments is List<Document<T>> list ? list : base.mDocuments.ToList();
            mTfidfMatrix = new(mDocuments.Count);
            mWordOccurences = new();
            mChampionLists = new();
            mTokenizer = Tokenizer.Create();
        }

        protected override void PreprocessTask()
        {
            using (StopWatch startTimer = StopWatchEx.StartNew(StopWatch.TickStyles.Milliseconds))
            {
                // Iterate over every word of every document to calculate term and document frequency
                for (int i = 0; i < mDocuments.Count; i++)
                {
                    Document<T> document = mDocuments[i];
                    Dictionary<string, double> embedding = new();
                    foreach (string word in mTokenizer.Tokenize(document.Title))
                    {
                        if (!embedding.ContainsKey(word))
                        {
                            embedding[word] = 0;
                        }
                        embedding[word] += PersistedSettings.kTitleWeight;

                        if (!mWordOccurences.ContainsKey(word))
                        {
                            mWordOccurences[word] = new();
                        }
                        mWordOccurences[word].Add(i);
                    }
                    foreach (string word in mTokenizer.Tokenize(document.Description))
                    {
                        if (!embedding.ContainsKey(word))
                        {
                            embedding[word] = 0;
                        }
                        embedding[word] += PersistedSettings.kDescriptionWeight;

                        if (!mWordOccurences.ContainsKey(word))
                        {
                            mWordOccurences[word] = new();
                        }
                        mWordOccurences[word].Add(i);
                    }
                    mTfidfMatrix.Add(embedding);
                    if (ShouldYield(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }

                // Iterate over every document again to turn TF vector embeddings into TF-IDF embeddings
                foreach (string word in new List<string>(mWordOccurences.Keys))
                {
                    double idf = Math.Log10((double)mDocuments.Count / mWordOccurences[word].Count);
                    foreach (int i in mWordOccurences[word])
                    {
                        mTfidfMatrix[i][word] = Math.Log10(1 + mTfidfMatrix[i][word]) * idf;
                    }
                    if (ShouldYield(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }

                // Build champion lists
                foreach (string word in mWordOccurences.Keys)
                {
                    mChampionLists[word] = mWordOccurences[word].OrderByDescending(x => mTfidfMatrix[x][word])
                                                                .Take(PersistedSettings.kChampionListLength)
                                                                .ToList();
                    if (ShouldYield(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }
            }
        }

        protected override IEnumerable<T> SearchTask(string query)
        {
            // Calculate TF-IDF vector embedding for the query, as well as its magnitude
            Dictionary<string, double> queryVector = new();
            double queryMagnitude = 0;
            foreach (var group in mTokenizer.Tokenize(query).GroupBy(word => word, (word, elements) => new { word, count = elements.Count() }))
            {
                if (mWordOccurences.ContainsKey(group.word))
                {
                    // If there is only one document, we are guaranteed to have reached a matching word at this point
                    // Stop early and return it to avoid calculating a TF-IDF of 0 and missing it
                    if (mDocuments.Count == 1)
                    {
                        return from document in mDocuments
                               select LogWeight(document, float.NaN);
                    }

                    double tfidf = Math.Log10(1 + group.count) * Math.Log10((double)mDocuments.Count / mWordOccurences[group.word].Count);
                    queryVector[group.word] = tfidf;
                    queryMagnitude += tfidf * tfidf;
                }
            }
            queryMagnitude = Math.Sqrt(queryMagnitude);

            // If the magnitude of the query vector is 0, then the query has no words in common with any documents in the corpus
            // Thus, we return an empty set of results
            if (queryMagnitude <= 0)
            {
                return Enumerable.Empty<T>();
            }

            // Go through the champion lists for each term in the query and calculate cosine similarity (dot product divided by product of magnitudes)
            // Between the query vector and each champion document vector
            Dictionary<int, double> championDocs = new(mDocuments.Count);
            foreach (string queryWord in queryVector.Keys)
            {
                foreach (int i in mChampionLists[queryWord])
                {
                    Dictionary<string, double> documentVector = mTfidfMatrix[i];
                    double docMagnitude = 0;
                    double dot = 0;
                    float numWords = 0;
                    foreach (string word in documentVector.Keys)
                    {
                        double tfidf = documentVector[word];
                        if (queryVector.ContainsKey(word))
                        {
                            numWords++;
                            dot += tfidf * queryVector[word];
                        }
                        docMagnitude += tfidf * tfidf;
                    }
                    if (numWords / queryVector.Count >= PersistedSettings.kQuerySimilarityThreshold)
                    {
                        docMagnitude = Math.Sqrt(docMagnitude);
                        championDocs[i] = docMagnitude != 0 ? dot / (docMagnitude * queryMagnitude) : 0;
                    }
                }
            }

            return from kvp in championDocs
                   where kvp.Value != 0
                   orderby kvp.Value descending
                   select LogWeight(mDocuments[kvp.Key], (float)kvp.Value);
        }
    }
}
