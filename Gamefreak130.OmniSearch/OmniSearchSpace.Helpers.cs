﻿using System.Text.RegularExpressions;

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

#if DEBUG
    public class DocumentLogger : Logger<Tuple>
    {
        public static readonly DocumentLogger sInstance = new();

        private readonly System.Text.StringBuilder mLog = new();

        private int mCount;

        public override void Log(Tuple input)
        {
            mCount++;
            Document<object> document = input.mParam1 as Document<object>;
            float weight = (float)input.mParam2;
            mLog.AppendLine($"{document.Title}\n{document.Description}\n{weight}\n");
        }

        public void WriteLog()
        {
            if (mLog.Length > 0)
            {
                base.WriteLog(mLog);
                mLog.Remove(0, mLog.Length);
                mCount = 0;
            }
        }

        protected override string WriteNotification() => $"Search results logged: {mCount} documents found";
    }
#endif
}