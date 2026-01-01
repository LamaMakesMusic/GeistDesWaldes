using System;
using System.Globalization;

namespace GeistDesWaldes.Citations
{
    [Serializable]
    public class Citation
    {
        public int ID;
        public string Author;
        public string Content;
        public DateTime Date;

        public Citation()
        {

        }
        public Citation(string author, string content, DateTime creationDate)
        {
            Author = author;
            Content = content;
            Date = creationDate;
        }

        public override string ToString()
        {
            return $"#{ID:000} | \"{Content}\" - {Author} | {Date:dd.MM.yyyy}";
        }

        public string ToStringHeader(CultureInfo info)
        {
            return $"#{ID:000} | {Date.ToString("dd. MMMM yyyy", info)}";
        }

        public string ToStringBody()
        {
            return $"\"{Content}\" - {Author}";
        }
    }
}
