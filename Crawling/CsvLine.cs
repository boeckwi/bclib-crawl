using FileHelpers;

namespace Crawling
{
    [DelimitedRecord(";"), IgnoreFirst(1)]
    public class CsvLine
    {
        [FieldQuoted('"')] 
        public string Title;
        [FieldQuoted('"')]
        public string Subtitle;
        [FieldQuoted('"')] 
        public string Publisher;

        public int Year;
        
        [FieldQuoted('"')]
        public bool Verliehen;

        public int BggId;
        public int BggRank;
        public float BggRating;
    }
}
