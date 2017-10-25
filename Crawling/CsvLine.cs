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

        public int Year;
        
        [FieldQuoted('"')]
        public bool Verliehen;
    }
}
