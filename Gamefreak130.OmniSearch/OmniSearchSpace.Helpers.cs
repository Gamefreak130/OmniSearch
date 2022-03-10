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
            Title = title ?? "";
            Description = description ?? "";
            Tag = tag;
        }
    }

    public interface ITokenizer
    {
        IEnumerable<string> Tokenize(string input);
    }

    public abstract class Tokenizer : ITokenizer
    {
        //language=regex
        protected const string kCharsToRemove = @"['’`‘#%™。・「」]";

        public abstract IEnumerable<string> Tokenize(string input);

        public static ITokenizer Create() => StringTable.GetLocale() switch
        {
            "ja-jp" or "zh-cn" or "zh-tw" or "th-th"  => new CharacterTokenizer(),
            _                                         => new EnglishTokenizer()
        };
    }

    public class EnglishTokenizer : Tokenizer
    {
        //language=regex
        private const string kTokenSplitter = @"[,\-_\\/\.!?;:""”“…()—\s]+";

        // TODO See if we can optimize
        public override IEnumerable<string> Tokenize(string input)
            => Regex.Split(Regex.Replace(input.ToLower(), kCharsToRemove, ""), kTokenSplitter).Where(token => token.Length > 0);
    }

    public class CharacterTokenizer : Tokenizer
    {
        public override IEnumerable<string> Tokenize(string input)
            => from character in Regex.Replace(input.ToLower(), kCharsToRemove, "")
               select character.ToString();
    }
}
