using Sims3.Metadata;
using System.Text.RegularExpressions;

namespace Gamefreak130.OmniSearchSpace.Helpers
{
    public readonly struct PatternInfo
    {
        public ResourceKey Key { get; }

        public Complate Complate { get; }

        public ColorInfo ColorInfo { get; }

        public PatternInfo(ResourceKey key, Complate complate, ColorInfo colorInfo)
        {
            Key = key;
            Complate = complate;
            ColorInfo = colorInfo;
        }
    }

    public class SearchGroup
    {
        public List<string> QueryHistory { get; } = new();

        public bool Collapsed { get; set; } = PersistedSettings.kCollapseSearchBarByDefault;
    }

    public class Document<T> : IEquatable<Document<T>>
    {
        public string Title { get; }

        public string Description { get; }

        public T Tag { get; }

        public Document(string title, string description, T tag)
        {
            Title = title ?? "";
            Description = description ?? "";
            Tag = tag;
        }

        public bool Equals(Document<T> otherDoc) => otherDoc.Title == Title && otherDoc.Description == Description;

        public override bool Equals(object o) => o is Document<T> otherDoc && Equals(otherDoc);

        public override int GetHashCode() => Title.GetHashCode() ^ Description.GetHashCode();

        public static bool operator ==(Document<T> doc1, Document<T> doc2) => doc1.Equals(doc2);

        public static bool operator !=(Document<T> doc1, Document<T> doc2) => !doc1.Equals(doc2);
    }

    public interface ITokenizer
    {
        IEnumerable<string> Tokenize(string input);
    }

    public abstract class Tokenizer : ITokenizer
    {
        //language=regex
        protected const string kCharsToRemove = @"['’`‘#%™。・「」¡¿]";

        public abstract IEnumerable<string> Tokenize(string input);

        public static ITokenizer Create() => StringTable.GetLocale() switch
        {
            "ja-jp" or "zh-cn" or "zh-tw" or "th-th"  => new CharacterTokenizer(),
            _                                         => new EnglishTokenizer()
        };
    }

    public class EnglishTokenizer : Tokenizer
    {
        private readonly char[] kTokenSplitter = { ' ', ',', '-', '_', '\\', '/', '.', '!', '?', ';', ':', '"', '”', '“', '…', '(', ')', '—', '\t', '\v', '\r', '\f' };

        public override IEnumerable<string> Tokenize(string input)
            => Regex.Replace(input.ToLower(), kCharsToRemove, "")
                    .Split(kTokenSplitter)
                    .Where(token => token.Length > 0);
    }

    public class CharacterTokenizer : Tokenizer
    {
        public override IEnumerable<string> Tokenize(string input)
            => from character in Regex.Replace(input.ToLower(), kCharsToRemove, "")
               select character.ToString();
    }

//#if DEBUG
    public class DocumentLogger : Logger<Tuple>
    {
        public static readonly DocumentLogger sInstance = new();

        private readonly System.Text.StringBuilder mLog = new();

        private int mCount;

        public override void Log(Tuple input) => throw new NotSupportedException();

        public void Log<T>(Tuple input)
        {
            mCount++;
            Document<T> document = input.mParam1 as Document<T>;
            float weight = (float)input.mParam2;
            mLog.AppendLine($"{document.Title}\n{document.Description}\n{weight}\n");
        }

        public void WriteLog(string query)
        {
            if (!string.IsNullOrEmpty(query))
            {
                base.WriteLog(mLog.Insert(0, $"Query: {query}\n\nResults:\n\n"));
                mLog.Remove(0, mLog.Length);
                mCount = 0;
            }
        }

        protected override string WriteNotification() => $"Search results logged: {mCount} documents found";
    }
//#endif
}
