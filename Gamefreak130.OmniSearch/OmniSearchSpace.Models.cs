using Gamefreak130.OmniSearchSpace.Helpers;
using System;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
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
        IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query);
    }

    public abstract class SearchModel<T> : ISearchModel<T>
    {
        // TODO More robust tokenizer for languages other than English

        //language=regex
        protected const string TOKEN_SPLITTER = @"[,\-_\\/\.!?;:""”“'…()—\s]+";

        //language=regex
        protected const string CHARS_TO_REMOVE = @"['’`‘#%]";

        public abstract IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query);

        protected T LogWeight(Document<T> document, float weight)
        {
#if DEBUG
            Debugger.Log(0, "", $"{document.Title}\n{document.Description}\n{weight}\n\n");
#endif
            return document.Tag;
        }
    }

    public class ExactMatch<T> : SearchModel<T>
    {
        public override IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query) 
            => from document in documents
               // Little trick I learned from StackOverflow to efficiently count substring occurrences
               let titleWeight = (document.Title.Length - document.Title.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kTitleWeight
               let descWeight = (document.Description.Length - document.Description.Replace(query.ToLower(), "").Length) / query.Length * PersistedSettings.kDescriptionWeight
               let weight = titleWeight + descWeight
               where weight > 0
               orderby weight descending
               select LogWeight(document, weight);
    }

    public class TermFrequency<T> : SearchModel<T>
    {
        public override IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query)
            => from document in documents
               let queryTokens = Regex.Split(Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, ""), TOKEN_SPLITTER)
               let titleWeight = Regex.Split(Regex.Replace(document.Title, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Count(token => queryTokens.Contains(token)) * PersistedSettings.kTitleWeight
               let descWeight = Regex.Split(Regex.Replace(document.Description, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER).Count(token => queryTokens.Contains(token)) * PersistedSettings.kDescriptionWeight
               let weight = titleWeight + descWeight
               where weight > 0
               orderby weight descending
               select LogWeight(document, weight);
    }

    public class TFIDFUnigram<T> : SearchModel<T>
    {
        // CONSIDER maybe use a common abstract TFIDF class
        public override IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query)
        {
            query = Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, "");
            List<Document<T>> docList = documents.ToList();

            // TFIDFMatrix is a set of document vector embeddings formed from the term frequency of words in each document weighted using TF-IDF
            // The embedding itself is represented as a word dictionary, since the full vector of all words in the corpus would be extremely sparse
            List<Dictionary<string, double>> tfidfMatrix = new(docList.Count);

            // wordOccurences is a mapping of words to the documents containing them, allowing us to easily calculate IDF
            // Using a HashSet of document indices rather than a document frequency count makes it easier to avoid double counting words appearing multiple times in a single document
            Dictionary<string, HashSet<int>> wordOccurences = new();

            // Iterate over every word of every document to calculate term and document frequency
            for (int i = 0; i < docList.Count; i++)
            {
                Document<T> document = docList[i];
                Dictionary<string, double> embedding = new();
                foreach (string word in Regex.Split(Regex.Replace(document.Title, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER))
                {
                    if (!embedding.ContainsKey(word))
                    {
                        embedding[word] = default;
                    }
                    embedding[word] += PersistedSettings.kTitleWeight;

                    if (!wordOccurences.ContainsKey(word))
                    {
                        wordOccurences[word] = new();
                    }
                    wordOccurences[word].Add(i);
                }
                foreach (string word in Regex.Split(Regex.Replace(document.Description, CHARS_TO_REMOVE, ""), TOKEN_SPLITTER))
                {
                    if (!embedding.ContainsKey(word))
                    {
                        embedding[word] = default;
                    }
                    embedding[word] += PersistedSettings.kDescriptionWeight;

                    if (!wordOccurences.ContainsKey(word))
                    {
                        wordOccurences[word] = new();
                    }
                    wordOccurences[word].Add(i);
                }
                tfidfMatrix.Add(embedding);
            }

            // Iterate over every document again to turn TF vector embeddings into TF-IDF embeddings
            for (int i = 0; i < docList.Count; i++)
            {
                foreach (KeyValuePair<string, double> kvp in new List<KeyValuePair<string, double>>(tfidfMatrix[i]))
                {
                    tfidfMatrix[i][kvp.Key] = Math.Log10(1 + kvp.Value) * Math.Log10(docList.Count / wordOccurences[kvp.Key].Count);
                }
            }

            // Calculate TF-IDF vector embedding for the query, as well as its magnitude
            Dictionary<string, double> queryVector = new();
            double queryMagnitude = 0;
            foreach (var group in Regex.Split(query, TOKEN_SPLITTER).GroupBy(word => word, (word, elements) => new { word, count = elements.Count() }))
            {
                if (wordOccurences.ContainsKey(group.word))
                {
                    double tfidf = Math.Log10(1 + group.count) * Math.Log10(docList.Count / wordOccurences[group.word].Count);
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
            List<double> similarities = new(docList.Count);
            for (int i = 0; i < docList.Count; i++)
            {
                Dictionary<string, double> documentVector = tfidfMatrix[i];
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

            return docList.Select((doc, i) => new { doc, i })
                          .Where(x => similarities[x.i] != 0)
                          .OrderByDescending(x => similarities[x.i])
                          .Select(x => LogWeight(x.doc, (float)similarities[x.i]));
        }
    }

    /*public class TFIDFBigram : SearchModel
    {

    }*/
}
