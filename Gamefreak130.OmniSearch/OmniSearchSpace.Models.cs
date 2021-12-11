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

    /*public class TermFrequency<T> : SearchModel<T>
    {
        public TermFrequency(IEnumerable<Document<T>> documents) : base(documents)
        {
        }

        public override IEnumerable<T> Search(string query)
            => from document in mDocuments
               let queryTokens = Regex.Split(Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, ""), TOKEN_SPLITTER)
               let titleWeight = Regex.Split(Regex.Replace(document.Title, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Count(token => token.Length > 0 && queryTokens.Contains(token)) * PersistedSettings.kTitleWeight
               let descWeight = Regex.Split(Regex.Replace(document.Description, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Count(token => token.Length > 0 && queryTokens.Contains(token)) * PersistedSettings.kDescriptionWeight
               let weight = titleWeight + descWeight
               where weight > 0
               orderby weight descending
               select LogWeight(document, weight);
    }*/

    public class TFIDF<T> : SearchModel<T>
    {
        private new readonly List<Document<T>> mDocuments;

        // TFIDFMatrix is a set of document vector embeddings formed from the term frequency of words in each document weighted using TF-IDF
        // The embedding itself is represented as a word dictionary, since the full vector of all words in the corpus would be extremely sparse
        private readonly List<Dictionary<string, double>> mTfidfMatrix;

        // wordOccurences is a mapping of words to the documents containing them, allowing us to easily calculate IDF
        // Using a HashSet of document indices rather than a document frequency count makes it easier to avoid double counting words appearing multiple times in a single document
        readonly Dictionary<string, HashSet<int>> mWordOccurences;

        public TFIDF(IEnumerable<Document<T>> documents) : base(documents)
        {
            mDocuments = base.mDocuments.ToList();
            mTfidfMatrix = new(mDocuments.Count);
            mWordOccurences = new();

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
            for (int i = 0; i < mDocuments.Count; i++)
            {
                foreach (KeyValuePair<string, double> kvp in new List<KeyValuePair<string, double>>(mTfidfMatrix[i]))
                {
                    mTfidfMatrix[i][kvp.Key] = Math.Log10(1 + kvp.Value) * Math.Log10(mDocuments.Count / mWordOccurences[kvp.Key].Count);
                }
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

            // Calculate cosine similarity (dot product divided by product of magnitudes) between the query vector and each document vector
            List<double> similarities = new(mDocuments.Count);
            for (int i = 0; i < mDocuments.Count; i++)
            {
                Dictionary<string, double> documentVector = mTfidfMatrix[i];
                double docMagnitude = 0;
                double dot = 0;
                foreach (string word in documentVector.Keys)
                {
                    double tfidf = documentVector[word];
                    if (queryVector.ContainsKey(word))
                    {
                        dot += tfidf * queryVector[word];
                    }
                    docMagnitude += tfidf * tfidf;
                }
                docMagnitude = Math.Sqrt(docMagnitude);
                similarities.Add(docMagnitude != 0 ? dot / (docMagnitude * queryMagnitude) : 0);
            }

            return mDocuments.Select((doc, i) => new { doc, i })
                             .Where(x => similarities[x.i] != 0)
                             .OrderByDescending(x => similarities[x.i])
                             .Select(x => LogWeight(x.doc, (float)similarities[x.i]));
        }
    }

    /*public class TFIDFBigram<T> : SearchModel<T>
    {
        public override IEnumerable<T> Search(string query)
        {
            query = Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, "");
            List<Document<T>> docList = documents.ToList();

            // TFIDFMatrix is a set of document vector embeddings formed from the term frequency of bigrams in each document weighted using TF-IDF
            // The embedding itself is represented as a bigram dictionary, since the full vector of all bigrams in the corpus would be extremely sparse
            List<Dictionary<(string, string), double>> tfidfMatrix = new(docList.Count);

            // wordOccurences is a mapping of bigrams to the documents containing them, allowing us to easily calculate IDF
            // Using a HashSet of document indices rather than a document frequency count makes it easier to avoid double counting bigrams appearing multiple times in a single document
            Dictionary<(string, string), HashSet<int>> wordOccurences = new();

            // Iterate over every bigram of every document to calculate term and document frequency
            for (int i = 0; i < docList.Count; i++)
            {
                Document<T> document = docList[i];
                Dictionary<(string, string), double> embedding = new();
                string[] words = Regex.Split(Regex.Replace(document.Title, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER);
                for (int j = 0; j <= words.Length; j++)
                {
                    (string, string) bigram = (j > 0 ? words[j - 1] : null, 
                                               j < words.Length ? words[j] : null);

                    if (bigram.Item1?.Length == 0 || bigram.Item2?.Length == 0)
                    {
                        continue;
                    }

                    if (!embedding.ContainsKey(bigram))
                    {
                        embedding[bigram] = 0;
                    }
                    embedding[bigram] += PersistedSettings.kTitleWeight;

                    if (!wordOccurences.ContainsKey(bigram))
                    {
                        wordOccurences[bigram] = new();
                    }
                    wordOccurences[bigram].Add(i);
                }

                words = Regex.Split(Regex.Replace(document.Description, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER);
                for (int j = 0; j <= words.Length; j++)
                {
                    (string, string) bigram = (j > 0 ? words[j - 1] : null,
                                               j < words.Length ? words[j] : null);

                    if (bigram.Item1?.Length == 0 || bigram.Item2?.Length == 0)
                    {
                        continue;
                    }

                    if (!embedding.ContainsKey(bigram))
                    {
                        embedding[bigram] = 0;
                    }
                    embedding[bigram] += PersistedSettings.kDescriptionWeight;

                    if (!wordOccurences.ContainsKey(bigram))
                    {
                        wordOccurences[bigram] = new();
                    }
                    wordOccurences[bigram].Add(i);
                }
                tfidfMatrix.Add(embedding);
            }

            // Iterate over every document again to turn TF vector embeddings into TF-IDF embeddings
            for (int i = 0; i < docList.Count; i++)
            {
                foreach (var kvp in new List<KeyValuePair<(string, string), double>>(tfidfMatrix[i]))
                {
                    tfidfMatrix[i][kvp.Key] = Math.Log10(1 + kvp.Value) * Math.Log10(docList.Count / wordOccurences[kvp.Key].Count);
                }
            }

            // Calculate TF-IDF vector embedding for the query, as well as its magnitude
            Dictionary<(string, string), double> queryVector = new();
            string[] queryWords = Regex.Split(query, TOKEN_SPLITTER);
            for (int j = 0; j <= queryWords.Length; j++)
            {
                (string, string) bigram = (j > 0 ? queryWords[j - 1] : null,
                                               j < queryWords.Length ? queryWords[j] : null);

                if (wordOccurences.ContainsKey(bigram))
                {
                    if (!queryVector.ContainsKey(bigram))
                    {
                        queryVector[bigram] = 0;
                    }
                    queryVector[bigram] += 1;
                }
            }

            double queryMagnitude = 0;
            foreach (var kvp in new List<KeyValuePair<(string, string), double>>(queryVector))
            {
                double tfidf = Math.Log10(1 + kvp.Value) * Math.Log10(docList.Count / wordOccurences[kvp.Key].Count);
                queryVector[kvp.Key] = tfidf;
                queryMagnitude += tfidf * tfidf;
            }
            queryMagnitude = Math.Sqrt(queryMagnitude);

            // If the magnitude of the query vector is 0, then the query has no bigrams in common with any documents in the corpus
            // Thus, we return an empty set of results
            if (queryMagnitude <= 0)
            {
                return new T[0];
            }

            // Calculate cosine similarity (dot product divided by product of magnitudes) between the query vector and each document vector
            List<double> similarities = new(docList.Count);
            for (int i = 0; i < docList.Count; i++)
            {
                Dictionary<(string, string), double> documentVector = tfidfMatrix[i];
                double docMagnitude = 0;
                double dot = 0;
                foreach ((string, string) bigram in documentVector.Keys)
                {
                    double tfidf = documentVector[bigram];
                    if (queryVector.ContainsKey(bigram))
                    {
                        dot += tfidf * queryVector[bigram];
                    }
                    docMagnitude += tfidf * tfidf;
                }
                docMagnitude = Math.Sqrt(docMagnitude);
                similarities.Add(docMagnitude != 0 ? dot / (docMagnitude * queryMagnitude) : 0);
            }

            return docList.Select((doc, i) => new { doc, i })
                          .Where(x => similarities[x.i] != 0)
                          .OrderByDescending(x => similarities[x.i])
                          .Select(x => LogWeight(x.doc, (float)similarities[x.i]));
        }
    }*/
}
