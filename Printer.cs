using HtmlAgilityPack;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace MySchemaApp
{
    internal class Printer
    {
        static public string StartDateToday(string url)
        {
            //Probably not a valid url.
            if (!url.Contains("startDatum")) return url;

            //Replaces startDatum=2020-01-01 with startDatum=idag.
            string updatedUrl = Regex.Replace(url, @"(startDatum=)[^&]*", "$1idag");
            return updatedUrl;
        }
        static public string CleanText(string text)
        {
            // Removes &nbsp;, &amp;, and such.
            string cleanText = HtmlEntity.DeEntitize(text);

            // Replaces tabs and such with empty space.
            cleanText = Regex.Replace(cleanText, @"\s+", " ");

            return cleanText.Trim();
        }

        //static public DateTime ParseDate(string dayAndDate)
        //{
        //    // Parses a date like 19 aug and return a DateTime object.

        //    int dummyYear = DateTime.Now.Year; // Won't be displayed, just needed for parsing.

        //    // Jan didn't work for swedish culture (it wants jan), Maj didn't work for english culture (it wants May), so I'm using both.
        //    var cultures = new[]
        //    {
        //        new CultureInfo("sv-SE"), // Swedish
        //        new CultureInfo("en-US")  // English
        //    };

        //    foreach (var culture in cultures)
        //    {
        //        if (DateTime.TryParseExact(dayAndDate + " " + dummyYear, "d MMM yyyy", culture, DateTimeStyles.None, out DateTime dt)) return dt;
        //    }
        //    throw new FormatException($"Unable to parse date: {dayAndDate}");
        //}

        static public void CustomPrintTable(HtmlNodeCollection rows)
        {
            if (rows == null) return;

            string day = "";
            string lastDate = "";

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells == null) continue;
                CustomPrintTableRowData(cells, day, lastDate);

                // Tracks current day and date in case of empty cells. Can happen
                // due to two or more consecutive lessons on the same day.
                // Example:
                // | Mån | 19 aug | 08:00 | 10:00 | Matematik | IK205G | 
                //|     |        | 10:00 | 12:00 | Matematik | IK205G |
                if (cells.Count > 1 && !string.IsNullOrEmpty(CleanText(cells[0].InnerText))) day = CleanText(cells[0].InnerText);
                if (cells.Count > 1 && !string.IsNullOrEmpty(CleanText(cells[1].InnerText))) lastDate = CleanText(cells[1].InnerText);
            }
        }

        static public void DefaultPrintTable(HtmlNodeCollection rows)
        {
            if (rows == null) return;
            foreach (var row in rows)
            {
                var cells = row.SelectNodes("td");
                if (cells == null) continue;
                DefaultPrintTableRowData(cells);
            }
        }

        static public void CustomPrintTableRowData(HtmlNodeCollection cells, string lastDay, string lastDate)
        {
            // Takes the row and and only shows what I want to have
            // displayed. I don't care much for who the day's teacher might be.

            // Probably a header row like "Vecka 4, 2026"
            if (cells.Count == 1)
            {
                Console.WriteLine(CleanText(cells[0].InnerText));
                return;
            }

            // Bigger explanation in CustomPrintTable(HtmlNodeCollection rows).
            // TLDR: applies value when there are none.
            if (string.IsNullOrEmpty(CleanText(cells[1].InnerText))) cells[1].InnerHtml = lastDay;
            if (string.IsNullOrEmpty(CleanText(cells[2].InnerText))) cells[2].InnerHtml = lastDate;

            // Calls attention to groups.
            if (!string.IsNullOrEmpty(CleanText(cells[5].InnerText))) cells[5].InnerHtml = "*GRUPP " + CleanText(cells[5].InnerText + "*");

            cells.Remove(6); // Removes Teacher column.
            cells.Remove(0); // Removes mystery column that says "A" during exam days.

            // Prints text for each cell.
            foreach (var cell in cells)
            {
                string cellText = CleanText(cell.InnerText);
                if (!string.IsNullOrWhiteSpace(cellText)) Console.Write(cellText + " | ");
            }
            Console.WriteLine(" ");
        }

        static public void DefaultPrintTableRowData(HtmlNodeCollection cells)
        {
            // Probably a header row like "Vecka 4, 2026"
            if (cells.Count == 1)
            {
                Console.WriteLine(CleanText(cells[0].InnerText));
                return;
            }

            // Prints text for each cell.
            foreach (var cell in cells)
            {
                string cellText = CleanText(cell.InnerText);
                if (!string.IsNullOrWhiteSpace(cellText)) Console.Write(cellText + " | ");
            }
            Console.WriteLine(" ");
        }

        static public async Task<string> AutoSetUrlStartDate(string currentUrl)
        {
            //This method only runs when the user has not set a start date in the url. In this case 
            //we want it to default to whenever the first lesson of the chosen course is scheduled. The solution for this
            //therefore became to check the number of rows the url would have returned and compare it to the number of rows
            //the same url 7 days prior would have returned and return the url with the most rows. This is based on
            //assumptions but is a good solution for the problem of not knowing when the first lesson is scheduled. 

            //Gets the number of rows the current url would have returned.
            List<HtmlNode> currentUrlNodes = await GetListOfNodes(currentUrl);

            DateTime newDate = DateTime.Today;

            //Gets the number of rows the same url would have returned if it had it's start date set to 7 days ago.
            string updatedUrl = currentUrl.Replace("startDatum=idag", "startDatum=" + newDate.AddDays(-7).ToString("yyyy-MM-dd"));
            List<HtmlNode> updatedUrlNodes = await GetListOfNodes(updatedUrl);

            //If the updated url has more rows than the current url
            while (updatedUrlNodes.Count > currentUrlNodes.Count)
            {
                //Sets the values for the next loop.
                currentUrl = updatedUrl;
                currentUrlNodes = updatedUrlNodes;
                newDate = newDate.AddDays(-7);

                //Updates the url to have a start date 7 days earlier than the previous loop.
                updatedUrl = currentUrl.Replace(
                    "startDatum=" + newDate.ToString("yyyy-MM-dd"),
                    "startDatum=" + newDate.AddDays(-7).ToString("yyyy-MM-dd"));

                updatedUrlNodes = await GetListOfNodes(updatedUrl);
            }

            //If it never changed, just set it to todays date instead of always "today" to avoid logic problems.
            if (currentUrl.Contains("startDatum=idag"))
            {
                return currentUrl.Replace("startDatum=idag", "startDatum=" + DateTime.Today.ToString("yyyy-MM-dd"));
            }

            //Returns the url with the most rows.
            return currentUrl;
        }

        static public async Task<List<HtmlNode>> GetListOfNodes(string url)
        {
            using var http = new HttpClient();

            var html = await http.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            var schemaTable = doc.DocumentNode.SelectSingleNode("//table[@class='schemaTabell']");

            if (schemaTable == null) return new List<HtmlNode>();
            
            var schemaTableRows = schemaTable.SelectNodes("./tr");

            return schemaTableRows.ToList();
        }
    }
}
