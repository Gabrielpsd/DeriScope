public static class ProgramInterface
{
    public static async Task<List<BackgroundOption>> MapsBackgroundOptions(string stockTicker, AppConfig appConfig,DataBaseManager _database)
    {
        List<BackgroundOption> monitoredTopics = new List<BackgroundOption>();
        Logger.Log("Conectin background Data");

        var URLRequest = $"{appConfig.BaseUrl}{stockTicker}";
        Logger.Log("Connectin data for stock: " + stockTicker);
        var data = await Web_Scrapper.GetData(URLRequest);
        Logger.Log("Data received for stock: " + data.Substring(0, Math.Min(20, data.Length)) + "..."); // Log the first 20 characters of the data
        var callOptions = await OptionMapper.extractCallDataFromHTMLAsync(data, appConfig.CallOptionTableOnWeb, _database);
        var putOptions = await OptionMapper.extractPutDataFromHTMLAsync(data, appConfig.PutOptionTableOnWeb, _database);

        Logger.Log("Options data extracted successfully for ticker: ");

        if (callOptions == null && putOptions == null)
        {
            Logger.Log("No options found for stock: " + stockTicker);
            return monitoredTopics;
        }

        foreach (var option in callOptions)
        {
            monitoredTopics.Add(new BackgroundOption
            {
                stock = stockTicker,
                optionTicker = option.Codigo,
                parameter = appConfig.AlertControl.ParameterControl,
            });
        }

        foreach(var option in putOptions ?? Enumerable.Empty<OptionData>())
        {
            monitoredTopics.Add(new BackgroundOption
            {
                stock = stockTicker,
                optionTicker = option.Codigo,
                parameter = appConfig.AlertControl.ParameterControl,
            });
        }
      

        return monitoredTopics;
    }

}
