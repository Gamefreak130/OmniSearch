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
        public override IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query)
        {
            // TODO Better documentation
            query = Regex.Replace(query.ToLower(), CHARS_TO_REMOVE, "");
            List<Document<T>> docList = documents.ToList();
            List<Dictionary<string, double>> tfidfMatrix = new(docList.Count);
            Dictionary<string, Dictionary<int, object>> wordOccurences = new();

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
                    wordOccurences[word][i] = null;
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
                    wordOccurences[word][i] = null;
                }
                tfidfMatrix.Add(embedding);
            }
            for (int i = 0; i < docList.Count; i++)
            {
                foreach (var group in new Dictionary<string, double>(tfidfMatrix[i]).Select(kvp => new { word = kvp.Key, count = kvp.Value }))
                {
                    tfidfMatrix[i][group.word] = Math.Log10(1 + group.count) * Math.Log10(docList.Count / wordOccurences[group.word].Count);
                }
            }

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

            if (queryMagnitude > 0)
            {
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
            return new T[0];
        }
    }

    /*public class TFIDFBigram : SearchModel
    {

    }*/
}
