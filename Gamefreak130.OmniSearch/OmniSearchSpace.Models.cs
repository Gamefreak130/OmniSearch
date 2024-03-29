﻿using System.Text.RegularExpressions;

namespace Gamefreak130.OmniSearchSpace.Models
{
    public interface ISearchModel : IDisposable
    {
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

        private bool mYieldPreprocessing;

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

        public SearchModel(IEnumerable<Document<T>> documents, bool yielding = true)
        {
            mDocuments = documents;
            mYieldPreprocessing = yielding;
        }


        public void Dispose()
        {
            ModelPreprocessTask = null;
#if DEBUG
            SimpleMessageDialog.Show("Disposed", "model");
#endif
        }

#if DEBUG
        ~SearchModel() => SimpleMessageDialog.Show("Finalized", "model");
#endif

        protected bool ShouldYieldPreprocessing(StopWatch startTimer)
            => mYieldPreprocessing && startTimer.GetElapsedTimeFloat() >= 1000f / PersistedSettings.kPreprocessingTickRate;

        public void Preprocess()
            => ModelPreprocessTask = TaskEx.Run(PreprocessTask);

        protected virtual void PreprocessTask()
        {
        }

        public IEnumerable<T> Search(string query)
        {
            try
            {
                ProgressDialog.Show(Localization.LocalizeString("Ui/Caption/Global:Processing"), UIManager.sDarkenBackground is null || !UIManager.sDarkenBackground.Visible);

                if (string.IsNullOrEmpty(query))
                {
                    return from document in mDocuments
                           select document.Tag;
                }

                if (ModelPreprocessTask is not null && !ModelPreprocessTask.IsCompleted)
                {
                    TaskEx.Delay((uint)(ProgressDialog.kProgressDialogDelay * 1000))
                          .ContinueWith(_ => mYieldPreprocessing = false);

                    ModelPreprocessTask.Await();
                }
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
            if (PersistedSettings.kEnableLogging)
            {
                DocumentLogger.sInstance.Log<T>(new(document, weight));
            }
            return document.Tag;
        }
    }

    public class ExactMatch<T> : SearchModel<T>
    {
        public ExactMatch(IEnumerable<Document<T>> documents, bool yielding = true) : base(documents, yielding)
        {
        }

        protected override IEnumerable<T> SearchTask(string query) 
            => from document in mDocuments
               // Little trick I learned from StackOverflow to efficiently count substring occurrences
               // https://stackoverflow.com/questions/541954/how-would-you-count-occurrences-of-a-string-actually-a-char-within-a-string
               let titleWeight = (float)(document.Title.Length - document.Title.ToLower().Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kTitleWeight
               let descWeight = (float)(document.Description.Length - document.Description.ToLower().Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kDescriptionWeight
               let weight = titleWeight + descWeight
               where weight > 0
               orderby weight descending
               select LogWeight(document, weight);
    }

    public class TFIDF<T> : SearchModel<T>
    {
        // TFIDFMatrix is a set of document vector embeddings formed from the term frequency of words in each document weighted using TF-IDF
        // The embedding itself is represented as a word dictionary, since the full vector of all words in the corpus would be extremely sparse
        private readonly List<Dictionary<string, double>> mTfidfMatrix;

        // WordOccurences is a mapping of words to the documents containing them, allowing us to easily calculate IDF
        // Using a HashSet of document indices rather than a document frequency count makes it easier to avoid double counting words appearing multiple times in a single document
        private readonly Dictionary<string, HashSet<int>> mWordOccurences;

        // ChampionLists is a set of the "top documents" in the corpus with the highest frequencies of a given word
        private readonly Dictionary<string, IEnumerable<int>> mChampionLists;

        private readonly ITokenizer mTokenizer;

        public TFIDF(IEnumerable<Document<T>> documents, bool yielding = true) : base(documents is List<Document<T>> ? documents : documents.ToList(), yielding)
        {
            mTfidfMatrix = new(mDocuments.Count());
            mWordOccurences = new();
            mChampionLists = new();
            mTokenizer = Tokenizer.Create();
        }

        protected override void PreprocessTask()
        {
            using (StopWatch startTimer = StopWatchEx.StartNew(StopWatch.TickStyles.Milliseconds))
            {
                // Iterate over every word of every document to calculate term and document frequency
                foreach (var current in mDocuments.Select((doc, i) => new { doc, i }))
                {
                    Dictionary<string, double> embedding = new();
                    foreach (string word in mTokenizer.Tokenize(current.doc.Title))
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
                        mWordOccurences[word].Add(current.i);
                    }
                    foreach (string word in mTokenizer.Tokenize(current.doc.Description))
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
                        mWordOccurences[word].Add(current.i);
                    }
                    mTfidfMatrix.Add(embedding);
                    if (ShouldYieldPreprocessing(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }

                // Iterate over every document again to turn TF vector embeddings into TF-IDF embeddings
                foreach (string word in new List<string>(mWordOccurences.Keys))
                {
                    double idf = Math.Log10((double)mDocuments.Count() / mWordOccurences[word].Count);
                    foreach (int i in mWordOccurences[word])
                    {
                        mTfidfMatrix[i][word] = Math.Log10(1 + mTfidfMatrix[i][word]) * idf;
                    }
                    if (ShouldYieldPreprocessing(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }

                // Build champion lists
                foreach (string word in mWordOccurences.Keys)
                {
                    mChampionLists[word] = mWordOccurences[word].OrderByDescending(x => mTfidfMatrix[x][word])
                                                                .Take(PersistedSettings.kChampionListLength);
                    if (ShouldYieldPreprocessing(startTimer))
                    {
                        TaskEx.Yield();
                        startTimer.Restart();
                    }
                }
            }
        }

        protected override IEnumerable<T> SearchTask(string query)
        {
            // Calculate frequency of full or sufficiently partial term matches
            Dictionary<string, double> queryVector = new();
            double queryMagnitude = 0;
            List<string> queryTokens = mTokenizer.Tokenize(query).ToList();
            foreach (var group in queryTokens.GroupBy(word => word, (word, elements) => new { word, count = elements.Count() }))
            {
                foreach (string fullWord in mWordOccurences.Keys.Where(x => x.StartsWith(group.word)))
                {
                    float matchPercentage = (float)group.word.Length / fullWord.Length;
                    if (matchPercentage >= PersistedSettings.kPartialMatchThreshold)
                    {
                        // If there is only one document, we are guaranteed to have reached a matching word at this point
                        // Stop early and return it to avoid calculating a TF-IDF of 0 and missing it
                        if (mDocuments.Count() == 1)
                        {
                            return from document in mDocuments
                                   select LogWeight(document, float.NaN);
                        }

                        if (!queryVector.ContainsKey(fullWord))
                        {
                            queryVector[fullWord] = 0;
                        }
                        queryVector[fullWord] += group.count * matchPercentage;
                    }
                }
            }

            // Calculate TF-IDF vector embedding for the query, as well as its magnitude
            foreach (string word in new List<string>(queryVector.Keys))
            {
                double tfidf = Math.Log10(1 + queryVector[word]) * Math.Log10((double)mDocuments.Count() / mWordOccurences[word].Count);
                queryVector[word] = tfidf;
                queryMagnitude += tfidf * tfidf;
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
            Dictionary<int, double> championDocs = new(mDocuments.Count());
            foreach (string queryWord in queryVector.Keys)
            {
                foreach (int i in mChampionLists[queryWord])
                {
                    Dictionary<string, double> documentVector = mTfidfMatrix[i];
                    double docMagnitude = 0;
                    double dot = 0;
                    HashSet<string> matchedTokens = new();
                    foreach (string word in documentVector.Keys)
                    {
                        double tfidf = documentVector[word];
                        if (queryVector.ContainsKey(word))
                        {
                            dot += tfidf * queryVector[word];
                            foreach (string token in queryTokens.Where(word.StartsWith))
                            {
                                matchedTokens.Add(token);
                            }
                        }
                        docMagnitude += tfidf * tfidf;
                    }
                    if (matchedTokens.Count / queryTokens.Count >= PersistedSettings.kQuerySimilarityThreshold)
                    {
                        docMagnitude = Math.Sqrt(docMagnitude);
                        championDocs[i] = docMagnitude != 0 ? dot / (docMagnitude * queryMagnitude) : 0;
                    }
                }
            }

            return from kvp in championDocs
                   where kvp.Value != 0
                   orderby kvp.Value descending
                   select LogWeight(mDocuments.Skip(kvp.Key).First(), (float)kvp.Value);
        }
    }

    internal class ExportBinSearchModel<T> : TFIDF<T>
    {
        public ExportBinSearchModel(IEnumerable<Document<T>> documents) : base(documents)
        {
        }

        protected override IEnumerable<T> SearchTask(string query)
        {
            Match lotSizeMatch = Regex.Match(query, @"(?:^|(?<=[^\d-]))(\d\d?)\s*[xX]\s*(\d\d?)(?!\d)");
            if (lotSizeMatch.Success && uint.TryParse(lotSizeMatch.Groups[1].Value, out uint width) && uint.TryParse(lotSizeMatch.Groups[2].Value, out uint height)
                && width <= 64 && width > 0 && height <= 64 && height > 0)
            {
                query = query.Remove(lotSizeMatch.Index, lotSizeMatch.Length)
                                .Insert(lotSizeMatch.Index, " ");

                IEnumerable<Document<T>> filteredDocs = from document in mDocuments
                                                        where LotSizeMatches(document, width, height)
                                                        select document;
                if (!Regex.IsMatch(query, @"\S"))
                {
                    return from document in filteredDocs
                            select document.Tag;
                }

                using (TFIDF<T> filteredSearchModel = new(filteredDocs))
                {
                    filteredSearchModel.Preprocess();
                    return filteredSearchModel.Search(query);
                }
            }
            return base.SearchTask(query);
        }

        private static bool LotSizeMatches(Document<T> document, uint x1, uint x2)
            => document.Tag switch
            {
                UIBinInfo info              => (info.LotSizeX == x1 && info.LotSizeY == x2) || (info.LotSizeX == x2 && info.LotSizeY == x1),
                ExportBinContents contents  => (contents.LotContentsSizeX == x1 && contents.LotContentsSizeY == x2) || (contents.LotContentsSizeX == x2 && contents.LotContentsSizeY == x1),
                _                           => throw new ArgumentException($"{document.Tag.GetType().Name} is not a valid Bin object")
            };
    }
}
