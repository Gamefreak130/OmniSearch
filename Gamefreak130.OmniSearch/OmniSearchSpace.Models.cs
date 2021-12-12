using Gamefreak130.OmniSearchSpace.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gamefreak130.OmniSearchSpace.Helpers
{
    public class Document<T>
    {
        public string Title { get; }

        public string Description { get; }

        public T Tag { get; }

        public Document(string title, string description, T tag)
        {
            Title = title?.ToLower() ?? "";
            Description = description?.ToLower() ?? "";
            Tag = tag;
        }
    }
}

namespace Gamefreak130.OmniSearchSpace.Models
{
    public interface ISearchModel<T>
    {
        IEnumerable<T> Search(string query);
    }

    public abstract class SearchModel<T> : ISearchModel<T>
    {
        // TODO More robust tokenizer for languages other than English

        //language=regex
        protected const string TOKEN_SPLITTER = @"[,\-_\\/\.!?;:""”“…()—\s]+";

        //language=regex
        protected const string CHARS_TO_REMOVE = @"['’`‘#%]";

        protected IEnumerable<Document<T>> mDocuments;

        public SearchModel(IEnumerable<Document<T>> documents)
            => mDocuments = documents;

        public abstract IEnumerable<T> Search(string query);

        protected T LogWeight(Document<T> document, float weight)
        {
            Debugger.Log(0, "", $"{document.Title}\n{document.Description}\n{weight}\n\n");
            return document.Tag;
        }
    }

    public class ExactMatch<T> : SearchModel<T>
    {
        public ExactMatch(IEnumerable<Document<T>> documents) : base(documents)
        {
        }

        public override IEnumerable<T> Search(string query) 
            => from document in mDocuments
               // Little trick I learned from StackOverflow to efficiently count substring occurrences
               // https://stackoverflow.com/questions/541954/how-would-you-count-occurrences-of-a-string-actually-a-char-within-a-string
               let titleWeight = (document.Title.Length - document.Title.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kTitleWeight
               let descWeight = (document.Description.Length - document.Description.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kDescriptionWeight
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

        private const double IDF_THRESHOLD = 0.6;

        public TFIDF(IEnumerable<Document<T>> documents) : base(documents)
        {
            mDocuments = base.mDocuments is List<Document<T>> list ? list : base.mDocuments.ToList();
            mTfidfMatrix = new(mDocuments.Count);
            mWordOccurences = new();
            mChampionLists = new();

            // Iterate over every word of every document to calculate term and document frequency
            for (int i = 0; i < mDocuments.Count; i++)
            {
                Document<T> document = mDocuments[i];
                Dictionary<string, double> embedding = new();
                foreach (string word in Regex.Split(Regex.Replace(document.Title, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Where(token => token.Length > 0))
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
                foreach (string word in Regex.Split(Regex.Replace(document.Description, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Where(token => token.Length > 0))
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
            }

            // Iterate over every document again to turn TF vector embeddings into TF-IDF embeddings
            foreach (string word in new List<string>(mWordOccurences.Keys))
            {
                double idf = Math.Log10(mDocuments.Count / mWordOccurences[word].Count);
                foreach (int i in mWordOccurences[word])
                {
                    if (idf < IDF_THRESHOLD)
                    {
                        mTfidfMatrix[i].Remove(word);
                    }
                    else
                    {
                        mTfidfMatrix[i][word] = Math.Log10(1 + mTfidfMatrix[i][word]) * idf;
                    }
                }
                if (idf < IDF_THRESHOLD)
                {
                    mWordOccurences.Remove(word);
                }
            }

            // Build champion lists
            foreach (string word in mWordOccurences.Keys)
            {
                mChampionLists[word] = mWordOccurences[word].OrderByDescending(x => mTfidfMatrix[x][word])
                                                            .Take(PersistedSettings.kChampionListLength)
                                                            .ToList();
            }
        }

        public override IEnumerable<T> Search(string query)
        {
            query = Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, "");

            // Calculate TF-IDF vector embedding for the query, as well as its magnitude
            Dictionary<string, double> queryVector = new();
            double queryMagnitude = 0;
            foreach (var group in Regex.Split(query, TOKEN_SPLITTER).GroupBy(word => word, (word, elements) => new { word, count = elements.Count() }))
            {
                if (mWordOccurences.ContainsKey(group.word))
                {
                    double tfidf = Math.Log10(1 + group.count) * Math.Log10(mDocuments.Count / mWordOccurences[group.word].Count);
                    queryVector[group.word] = tfidf;
                    queryMagnitude += tfidf * tfidf;
                }
            }
            queryMagnitude = Math.Sqrt(queryMagnitude);

            // If the magnitude of the query vector is 0, then the query has no words in common with any documents in the corpus
            // Thus, we return an empty set of results
            if (queryMagnitude <= 0)
            {
                return new T[0];
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

            return championDocs.Where(x => x.Value != 0)
                               .OrderByDescending(x => x.Value)
                               .Select(x => LogWeight(mDocuments[x.Key], (float)x.Value));
        }
    }
}
