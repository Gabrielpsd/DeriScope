using HtmlAgilityPack;
using System.Globalization;
using System.Net.Http;
using static DerivativeMonitor.MainWindow;

public static class Web_Scrapper
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task<string> GetData(string URlRequest)
    {

        try
        {
            Logger.Log("Sending HTTP GET request to URL: " + URlRequest);
            HttpResponseMessage response = await _httpClient.GetAsync(URlRequest);
            response.EnsureSuccessStatusCode();
            Logger.Log("Received successful response from URL: " + URlRequest);
            string content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching data for ticker: {ex.Message}");
            return "";
        }
    }

    public async static Task<string> ExtractTags(string HTML, string tag)
    {
        int start = HTML.IndexOf($"<{tag}>");
        int end = HTML.IndexOf($"</{tag}>");

        if (start < 0 || end < 0)
        {
            return string.Empty;
        }

        start += start + $"{tag}".Length; // jumping to the end of the opening tag
        return HTML.Substring(start, end - start);
    }
    public async static Task<List<List<string>>> ExtractTableById(string html, string tableId)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Select table by ID
        var table = doc.DocumentNode.SelectSingleNode($"//table[@id='{tableId}']");

        if (table == null)
            return new List<List<string>>();

        var result = new List<List<string>>();

        // Get all rows
        var rows = table.SelectNodes(".//tr");

        if (rows == null)
            return result;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("th|td");

            if (cells == null)
                continue;

            var rowData = new List<string>();

            foreach (var cell in cells)
            {
                rowData.Add(cell.InnerText.Trim());
            }

            result.Add(rowData);
        }

        return result;
    }

    public async static Task<List<OptionData>> MapToOptions(List<List<string>> table, DataBaseManager _dataBase)
    {

        var result = new List<OptionData>();

        foreach (var row in table.Skip(1)) // skip header
        {
            _dataBase.SaveOption(row[1], row[2], row[0], row[1], DateTime.Parse(row[3]), decimal.Parse(row[4], new CultureInfo("pt-BR")), row[5], row[6].Trim().ToUpper() == "SIM", row[7], "call");
            
            var option = new OptionData
            {
                Codigo = row[2],
                Vencimento = DateTime.Parse(row[3]),
                PrecoExercicio = decimal.Parse(row[4], new CultureInfo("pt-BR")),
            };

            result.Add(option);
        }

        return result;
    }

}
