using Gamefreak130.OmniSearchSpace.Helpers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
        public abstract IEnumerable<T> Search(IEnumerable<Document<T>> documents, string query);

        protected T LogWeight(Document<T> document, float weight)
        {
            Debugger.Log(0, "", $"{document.Title}: {weight}\n");
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

    /*public class TermFrequency : SearchModel
    {
        
    }

    public class TFIDFUnigram : SearchModel
    {

    }

    public class TFIDFBigram : SearchModel
    {

    }*/
}
