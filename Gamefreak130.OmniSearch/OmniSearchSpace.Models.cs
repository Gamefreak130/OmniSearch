using Gamefreak130.OmniSearchSpace.Helpers;
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
            Title = title;
            Description = description;
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
        //language=regex
        protected const string TOKEN_SPLITTER = @"[,\-_\\/\.!?\s]+";

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
        {
            query = query.ToLower();
            return from document in documents
                   // Little trick I learned from StackOverflow to efficiently count substring occurrences
                   let titleWeight = (document.Title.Length - document.Title.ToLower().Replace(query, "").Length) / query.Length * PersistedSettings.kTitleWeight
                   let descWeight = (document.Description.Length - document.Description.ToLower().Replace(query, "").Length) / query.Length * PersistedSettings.kDescriptionWeight
                   let weight = titleWeight + descWeight
                   where weight > 0
                   orderby weight descending
                   select LogWeight(document, weight);
        }
    }

    public class TermFrequency<T> : SearchModel<T>
    {
        public override IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query)
        {
            string[] queryTokens = Regex.Split(query.ToLower(), TOKEN_SPLITTER);
            return from document in documents
                   let titleWeight = Regex.Split(document.Title.ToLower(), TOKEN_SPLITTER).Where(token => queryTokens.Contains(token)).Count() * PersistedSettings.kTitleWeight
                   let descWeight = Regex.Split(document.Description.ToLower(), TOKEN_SPLITTER).Where(token => queryTokens.Contains(token)).Count() * PersistedSettings.kDescriptionWeight
                   let weight = titleWeight + descWeight
                   where weight > 0
                   orderby weight descending
                   select LogWeight(document, weight);
        }
    }

    /*public class TFIDFUnigram : SearchModel
    {

    }

    public class TFIDFBigram : SearchModel
    {

    }*/
}
