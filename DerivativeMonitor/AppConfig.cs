using System.Collections.ObjectModel;

public class AppConfig
{
    // ticker do ativo que se deseja obter as opções, ex: AAPL, MSFT, etc.
    public string Ticker { get; set; } = "";

    // nome da tabela onde estão as opções de venda (put) no site, ex: "puts-table", "put-options-table", etc.
    public string PutOptionTableOnWeb { get; set; } = "";

    // nome da tabela onde estão as opções de compra (call) no site, ex: "calls-table", "call-options-table", etc.
    public string CallOptionTableOnWeb { get; set; } = "";

    // URL base do site onde as opções estão listadas, ex: "https://finance.yahoo.com/quote/AAPL/options"
    public string BaseUrl { get; set; } = "";

    // caminho do banco de dados onde as opções serão salvas, ex: "options.db", "C:\\data\\options.db", etc.
    public string DatabasePath { get; set; } = "options.db";

    public string LogFileName { get; set; } = "Log";

    // No sistema do Profit os ativos são identificados por um código que é composto pelo ticker do ativo seguido de um sufixo específico para opções,
    // ex: "PETR" + "_B_0" = "PETR_B_0" para opções da Apple. O sufixo pode variar dependendo do sistema ou corretora, então é importante configurá-lo corretamente para garantir que as opções sejam identificadas corretamente no banco de dados e no sistema de negociação.
    public string TicketSuffix { get; set; } = "";

    // No Profit o sufixo das opções é diferente do sufixo das ações
    public string DerivativesSuffix { get; set; } = "";

    // intervalo de tempo em milisegundos para atualizar os dados das opções, ex: 60000 para atualizar a cada 60 segundos.
    public int RefreshIntervalMilliseconds { get; set; } = 60000;

    // intervalo de tempo em minutos para buscar novas opções, ex: 60 para buscar novas opções a cada 60 minutos.
    public int ScraperIntervalMinutes { get; set; } = 60;


    // opção para habilitar ou desabilitar logs dos passos do processo (início do scraping, início da atualização, etc.), ex: true para habilitar logs detalhados, false para desabilitar.
    public bool debugSteps { get; set; } = false;

    public bool generatesRandomData { get; set; } = false;

    public Dictionary<string, string> CallParametersToMonitor { get; set; } = new Dictionary<string, string>();

    public Dictionary<string, string> PutParametersToMonitor { get; set; } = new Dictionary<string, string>();

    public Dictionary<string, string> CallFieldFormats { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> PutFieldFormats { get; set; } = new Dictionary<string, string>();

    public UiColors Colors { get; set; } = new UiColors();

    public ChartSettings Chart { get; set; } = new ChartSettings();

    public AlertControl AlertControl { get; set; } = new AlertControl();
}

public class UiColors
{   public string OpenPrice { get; set; } = "#4CAF50";     // Green
    public string ClosePrice { get; set; } = "#F44336";     // Red
    public string StrikeColumn { get; set; } = "#D3D3D3"; // LightGray

    public string StrikeFontColor { get; set; } = "#000000"; // Black
    public string StrikeFontColorBar { get; set; } = "#000000"; // Black
    public string CallLine { get; set; } = "#E0E0E0";     // Light border

    public string CallFontColor { get; set; } = "#000000"; // Black
    public string CallFontColorBar { get; set; } = "#000000"; // Black
    public string PutLine { get; set; } = "#E0E0E0";     // Light border

    public string PutFontColor { get; set; } = "#000000"; // Black
    public string PutFontColorBar { get; set; } = "#000000"; // Black
    public string GridLine { get; set; } = "#E0E0E0";     // Light border

    public string ChartCall { get; set; } = "#4CAF50";     // Green

    public string ChartPut { get; set; } = "#F44336";     // Red

    public string ChartActualPrice { get; set; } = "#2196F3";     // Blue

}

public class ChartSettings {

    public string Parameter { get; set; } = "CAB";

    public string FieldFormatChart { get; set; } = "text";

    public string XAxisTitle { get; set; } = "Preço de Strike";

    public string YAxisTitle { get; set; } = "Quantidade de contratos";

    public double ChartWidth { get; set; } = 800;

}

// Used on the AlertControl class to specify which stocks to monitor for alerts.
public class StockToMonitor
{
    public string Symbol { get; set; } = "";
}

// AlertControl class to manage alert settings such as average control, sound and visual alerts, and email notifications.
public class AlertControl
{
    public float StandardDeviation { get; set; } = 1.5f; // Control for variance, default is 0.1

    public string StartDate { get; set; } = DateTime.Now.AddDays(-365).ToString("yyyy-MM-dd"); // Start date for alert control, default is 30 days ago

    public ObservableCollection<StockToMonitor> StocksToMonitor { get; set; } = new() ; // List of stocks to monitor for alerts, default is empty
    public string ParameterControl { get; set; } = "CAB"; // Parameter to control for alerts, default is "CAB"
    public string SoundFilePath { get; set; } = "alert.wav"; // Path to the sound file for alerts, default is "alert.wav"

    public bool EnableSoundAlert { get; set; } = true; // Enable or disable sound alerts, default is true

    public bool EnableSpeechAlert { get; set; } = true;

    public HistoricRanges HistoricRanges { get; set; } = new HistoricRanges(); // Ranges for historical data, default is 30 days

    public Dictionary<string, DateTime?> GetSelectedCutoffs()
    {
        var cutoffs = new Dictionary<string,DateTime?>();

        DateTime now = DateTime.Now;

        if (HistoricRanges.OneYear)
            cutoffs.Add("um Ano",now.AddYears(-1));

        if (HistoricRanges.SixMonths)
            cutoffs.Add("Seis meses", now.AddMonths(-6));

        if (HistoricRanges.ThreeMonths)
            cutoffs.Add("Três meses", now.AddMonths(-3));

        if (HistoricRanges.Total)
            cutoffs.Add("Total", DateTime.Parse("01/01/1900"));

        if (HistoricRanges.Custom)
            cutoffs.Add($"Customizado de {HistoricRanges.CustomDays} dias", now.AddDays(-HistoricRanges.CustomDays));

        return cutoffs;
    }
}


public class HistoricRanges        // each checkbox; several can be true at once
{
    public bool OneYear { get; set; }
    public bool SixMonths { get; set; }
    public bool ThreeMonths { get; set; } = true;
    public bool Total { get; set; }
    public bool Custom { get; set; }
    public int CustomDays { get; set; } = 30;
}