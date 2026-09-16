using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
/* Some maniputalations shoulb be done on the program , such manipualtes the HTML and the returns of the RTD 
 * the role of this class is modify the data and let it useful for the other classes of the program */
public static class OptionMapper
{
    /* This function shoul be used to extract the data within the HTML request made from the site that have all the options listed, 
     * the function receives the HTML as a string and the name of the tables where the call and put options are listed, 
     * then it returns an Options object with two lists of OptionData, one for call options and another for put options */
    public async static Task<List<OptionData>> extractCallDataFromHTMLAsync(string data, string CallOptionTableName,DataBaseManager _dataBase)
    {

        Logger.Log("Extracting call options from HTML...");
        var table = await Web_Scrapper.ExtractTableById(data, CallOptionTableName);
        var CallOptionsList = await Web_Scrapper.MapToOptions(table, _dataBase);

        return CallOptionsList;
    }

    public async static Task<List<OptionData>> extractPutDataFromHTMLAsync(string data, string PutOptionTableName, DataBaseManager _dataBase)
    {

        Logger.Log("Extracting puts options from HTML...");

        var table = await Web_Scrapper.ExtractTableById(data, PutOptionTableName);
        var PutOptionsList = await Web_Scrapper.MapToOptions(table, _dataBase);

        return PutOptionsList;
    }

    /* The options should be showed gruped by the strike price, so this function receives an Options object and returns a list of OptionRow, 
     * where each OptionRow represents a strike price and contains the call and put options that have that strike price */
    public static async Task<List<OptionsMonitored>> BuildOptionRows(List<OptionData> callOption, List<OptionData> putOption)
    {
        Logger.Log("Building option rows from options...");
        var dict = new Dictionary<decimal, OptionsMonitored>();


        // CALLS

        foreach (var call in callOption)
        {
            if (!dict.TryGetValue(call.PrecoExercicio, out var row))
            {
                row = new OptionsMonitored { Strike = call.PrecoExercicio };
                dict[call.PrecoExercicio] = row;
            }

            row.CallCodigo = call.Codigo;

        }

        // PUTS

        foreach (var put in putOption)
        {
            if (!dict.TryGetValue(put.PrecoExercicio, out var row))
            {
                row = new OptionsMonitored { Strike = put.PrecoExercicio };
                dict[put.PrecoExercicio] = row;
            }

            row.PutCodigo = put.Codigo;
        }
        
        return dict.Values
                   .OrderBy(x => x.Strike)
                   .ToList();
    }
}