public static class ProgramInterface
{
    public static async Task<List<BackgroundOption>> MapsBackgroundOptions(string stockTicker, AppConfig appConfig)
    {
        List<BackgroundOption> monitoredTopics = new List<BackgroundOption>();
        Logger.Log("Conectin background Data");


        var URLRequest = $"{appConfig.BaseUrl}{stockTicker}";
        Logger.Log("Connectin data for stock: " + stockTicker);
        var data = await Web_Scrapper.GetData(URLRequest);
        Logger.Log("Data received for stock: " + data.Substring(0, Math.Min(20, data.Length)) + "..."); // Log the first 20 characters of the data
        var options = OptionMapper.extractDataFromHTML(data, appConfig.PutOptionTableOnWeb, appConfig.CallOptionTableOnWeb);
        Logger.Log("Options data extracted successfully for ticker: " + stockTicker);


        Logger.Log("Populating background options");
        foreach(var option in options.CallOptions ?? Enumerable.Empty<OptionData>())
        {
            monitoredTopics.Add(new BackgroundOption
            {
                stock = stockTicker,
                optionTicker = option.Codigo,
                parameter = appConfig.AlertControl.ParameterControl,
            });
        }
        foreach(var option in options.PutOptions ?? Enumerable.Empty<OptionData>())
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

    public static async Task<Options> PopulatesOptionObject(string ticker, AppConfig config)
    {
        var URLRequest = $"{config.BaseUrl}{ticker}";
        // requesting data form web page
        Logger.Log("Requesting data from URL: " + URLRequest);
        var data = await Web_Scrapper.GetData(URLRequest);
        Logger.Log("Data received from URL: " + data.Substring(0, Math.Min(100, data.Length)) + "..."); // Log the first 100 characters of the data
        var options = OptionMapper.extractDataFromHTML(data, config.PutOptionTableOnWeb, config.CallOptionTableOnWeb);
        Logger.Log("Options data extracted successfully for ticker: " + ticker);

        return options;
    }

    public static void printOptions(Options options)
    {
        if (options.CallOptions != null)
            foreach (var callOption in options.CallOptions)
            {
                Console.WriteLine($"Call Option: {callOption.Codigo}, Price: {callOption.PrecoExercicio}");
            }
        if (options.PutOptions != null)
            foreach (var putOption in options.PutOptions)
            {
                Console.WriteLine($"Put Option: {putOption.Codigo}, Price: {putOption.PrecoExercicio}");
            }
    }
}
