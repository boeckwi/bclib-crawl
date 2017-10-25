using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Xml.XPath;
using FileHelpers;
using HtmlAgilityPack;

namespace Crawling
{
    class Program
    {
        const int FIRST_YEAR = 1950;
        const int LAST_YEAR = 2020;
        const int PAGE_SIZE = 25;

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Write("Download, Clear, Transform or Enrich? (d/c/t/e) ");
                switch (Console.ReadLine())
                {
                    case "c":
                        StartClear();
                        break;
                    case "d":
                        StartDownload();
                        break;
                    case "t":
                        StartTransform();
                        break;
                    case "e":
                        StartEnrich();
                        break;
                }

                Console.WriteLine("done");
                Console.ReadLine();
            }

        }

        static void StartEnrich()
        {
            var engine = new FileHelperEngine<CsvLine>();

            var file = Directory.GetFiles("output", "*.csv").OrderByDescending(x => x).FirstOrDefault();

            if (file == null)
            {
                Console.WriteLine("file not found");
                return;
            }

            var lines = engine.ReadFile(file);

            foreach (var line in lines)
            {
                if (line.BggId != 0) continue;

                var query = ToQueryString(new NameValueCollection
                {
                    { "type", "boardgame" },
                    { "search", line.Title.SplitAtLastIndexOf(':')[0].TrimEnd() },
                    //{ "exact", "1" }
                });
                //var uri = new Uri(@"https://boardgamegeek.com/xmlapi2/search" + query);
                var uri = new Uri(@"https://boardgamegeek.com/xmlapi/search" + query);
                XDocument doc;
                try
                {
                    doc = XDocument.Load(uri.ToString());
                }
                catch (WebException e)
                {
                    Console.WriteLine(e.Message);
                    break;
                }

                var items = doc.XPathSelectElements("//boardgames/boardgame").ToArray();
                var matches = items.Where(x => CompareElement(x, line.Year + "")).ToArray();
                var match = matches.FirstOrDefault();
                if (match != null)
                {
                    line.BggId = int.Parse(match.Attribute("objectid").Value);
                }
                else
                {
                    line.BggId = -1;
                }
                Console.WriteLine(line.Title + " -> " + line.BggId);
            }

            engine.WriteFile(file + ".enriched", lines);
        }

        static bool CompareElement(XElement x, string expected)
        {
            var el = x.Element("yearpublished");

            if (el == null) return false;

            return el.Value == expected;
        }

        static string ToQueryString(NameValueCollection nvc)
        {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value)))
                .ToArray();
            return "?" + string.Join("&", array);
        }
        static void StartClear()
        {
            if (Directory.Exists("work"))
                Directory.Delete("work", recursive: true);
        }

        static void StartTransform()
        {
            var engine = new FileHelperEngine<CsvLine>();
            engine.HeaderText = engine.GetFileHeader();

            List<CsvLine> lines = new List<CsvLine>();

            foreach (string path in Directory.GetFiles("work", "*.html"))
            {
                var html = File.ReadAllText(path);
                var csvLines = Transform(html);
                lines.AddRange(csvLines);
                Console.WriteLine("{0} titles in {1}", csvLines.Count, path);
            }

            CreateDirectory("output");

            engine.WriteFile(string.Format("output\\{0:yyyy-MM-dd HH.mm.ss}.csv", DateTime.Now), lines);
        }

        static void CreateDirectory(string name)
        {
            if (!Directory.Exists(name))
                Directory.CreateDirectory(name);
        }

        static void StartDownload()
        {
            CreateDirectory("work");

            for (int y = LAST_YEAR; y >= FIRST_YEAR; y--)
            {
                var offset = 1;
                while (true)
                {
                    var start = DateTime.Now;

                    string result = Download(y, offset);

                    if (ContainsData(result))
                    {
                        var path = string.Format("work\\year {0} offset {1}.html", y, offset);
                        Console.WriteLine("{0} - {1}", path, DateTime.Now - start);
                        File.WriteAllText(path, result);
                    }
                    else
                    {
                        Console.WriteLine("finished year {0}", y);
                        break;
                    }

                    offset += PAGE_SIZE;
                }
            }

        }

        static bool ContainsData(string result)
        {
            // Titel (50 bis 37)

            var r = Regex.Match(result, @"Titel \((\d+) bis (\d+)\)");

            if (!r.Success) return false;

            var a = int.Parse(r.Groups[1].Value);
            var b = int.Parse(r.Groups[2].Value);

            return b > 0 && a <= b;
        }


        static List<CsvLine> Transform(string html)
        {
            List<CsvLine> result = new List<CsvLine>();
            var root = new HtmlDocument();
            root.LoadHtml(html);


            var table = root.DocumentNode.Descendants("table")
                .FirstOrDefault(d => HasClass(d, "tab21"));

            if (table == null) return result;

            var rows = table.Descendants("tr").ToArray();

            foreach (var row in rows)
            {
                var datas = row.Descendants("td").Where(x => HasClass(x, "td01q1 td01x08n")).ToArray();
                if (!datas.Any())
                    continue;

                var name_cell = datas.ElementAtOrDefault(0);//<a href="ftitle.C?LANG=de&FUNC=full&SORTX=13&330238=YES"> <font color="black">Ben Hur <br>Hamburg: Universal Pictures Germany,<br><b>Filmkiste - Historisches</b></font></a>
                var year_cell = datas.ElementAtOrDefault(1); // 2017
                var status_cell = datas.ElementAtOrDefault(2); // <font class="p02x09b">Stadtb&#252;cherei</font> <br>   p02x09b=rot   p04x09b=grün

                try
                {
                    var year = year_cell.InnerText;
                    var name = HtmlEntity.DeEntitize(name_cell.Descendants("font").First().InnerText).RemoveMultipleWhitespaces();
                    var verliehen = HasClass(status_cell.Descendants("font").First(), "p02x09b");

                    var name_split = name.SplitAtLastIndexOf(',');
                    var title_split = name_split[0].SplitAtLastIndexOf(':');

                    result.Add(new CsvLine
                    {
                        Title = title_split[0],
                        Publisher = (title_split.ElementAtOrDefault(1) ?? "").TrimStart(),
                        Subtitle = name_split[1],
                        Year = int.Parse(year),
                        Verliehen = verliehen
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine("Skipping entry: {0}", e);
                }

                //Console.WriteLine("{0,-4} {1,-60} {2,-30}", verliehen ? "ROT" : "GRÜN", title_split[0], title_split[1]);
            }
            return result;
        }

        static bool HasClass(HtmlNode d, string classname)
        {
            return d.Attributes.Any(a => a.Name == "class" && a.Value == classname);
        }

        static string Download(int year, int frompos)
        {
            string result;
            using (var client = new WebClient())
            {
                var url = "https://katalog.medienzentrum-biberach.de/opax/query.C";
                var form_raw =
                    "LANG=de&FUNC=qsel&REG1=AW&FLD1=&CNN1=AND&SORTX=13&REG2=TW&FLD2=&CNN2=AND&REG3=DW&FLD3=&CNN3=AND&REG4=SG&FLD4=&CNN4=AND&REG5=PY&FLD5=2017&CNN5=AND&ZW=1&MT=9&SHOWSTAT=N&FROMPOS=" + frompos;
                var headers_raw =
                    @"Accept:text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8
Accept-Encoding:gzip, deflate, br
Accept-Language:de-DE,de;q=0.8,en-US;q=0.6,en;q=0.4
Cache-Control:max-age=0
Content-Type:application/x-www-form-urlencoded
Cookie:opaCok3=_+2017-!!-?FUNC=qsel&LANG=de&FLD5=2017&REG5=PY&CNN5=AND&ZW=1&MT=%2C9%2C&SORTX=13&NOHIS=1_+2017-!!-?FUNC=qsel&LANG=de&FLD5=2017&REG5=PY&CNN5=AND&ZW=1&MT=%2C9%2C&SORTX=13&NOHIS=1_knizia-!!-?FUNC=qsim&LANG=de&BI=knizia&SORTX=13&NOHIS=1B03B
Host:katalog.medienzentrum-biberach.de
Origin:https://katalog.medienzentrum-biberach.de
Referer:https://katalog.medienzentrum-biberach.de/opax/de/qsel.html.S
Upgrade-Insecure-Requests:1
User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/61.0.3163.100 Safari/537.36";

                //Connection:keep-alive
                //Content-Length:176

                form_raw = form_raw.Replace("2017", year + "");
                headers_raw = headers_raw.Replace("2017", year + "");


                var header_lines = headers_raw.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in header_lines)
                {
                    var splitted = line.SplitOnce(':');
                    client.Headers[splitted[0]] = splitted[1];
                }


                result = client.UploadString(url, form_raw);
            }
            return result;
        }
    }
}
