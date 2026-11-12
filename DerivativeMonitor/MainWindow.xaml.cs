using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Media;
using System.Numerics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using static RTDClient;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace DerivativeMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private int _lastHoverIndex = -1;                 // for the performance guard below
        private bool loopingIsRunning = false;
        private ScottPlot.Plottables.Crosshair _hoverCrosshair;
        public  ObservableCollection<Alert> _alerts { get; } = new();
        private AlertWindow? _alertWindow;
        private AppConfig _appConfig = new AppConfig();
        private List<BackgroundOption> _backgroundOptionList = new();
        
        //I'll only handle and modify the _charLookup
        private Dictionary<decimal, ChartRow> _chartLookup = new();
        private ChartHandler _chartHandler = new();
        private DataBaseManager _dataBaseManager = new DataBaseManager();
        // the _chartRows is the collection that is binded to the chart, so when I update it, the chart will be updated automatically
        private ObservableCollection<ChartRow> _chartRows = new();
        private CancellationTokenSource _cts = new();
        private ObservableCollection<OptionsMonitored> _optionsMonitored { get; set; } = new();
        SoundPlayer player = new SoundPlayer("Assets/Sounds/alert.wav");
        private List<ChartRow> _orderedChartRows = new(); // bars in the same order as the chart
        private RTDClient? _rtdClient = new RTDClient();
        private SpeechService _speechService = new SpeechService();
        private StockData _stockData = new();

        public record LoadStatus(int Percent, string Message);
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void Alerts_CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e)
        {
            if (_alerts.Count > 0)
            {
                ShowAlertWindow();
            }
            else
            {
                HideAlertWindow();
            }
        }

        private void ApplySettings()
        {
            TickerInput.Text = _appConfig.Ticker;
            BuildDynamicColumns();   // columns depend on CallParametersToMonitor + colors
        }

        private void BuildDynamicColumns()
        {
            Logger.Log("Building dynamic columns for DataGrid based on monitored parameters...");
            OptionsGrid.Columns.Clear();

            // ------------------------
            // CALL SIDE
            // ------------------------

            Logger.Log("Adding CALL side columns...");
            var reversedParams = _appConfig.CallParametersToMonitor.AsEnumerable().Reverse().ToList();
            var callBrush = ColorHelper.FromHex(_appConfig.Colors.CallLine);
            var putBrush = ColorHelper.FromHex(_appConfig.Colors.PutLine);
            var strikeBrush = ColorHelper.FromHex(_appConfig.Colors.StrikeColumn);

            var callStyle = new Style(typeof(TextBlock));

            //callStyle.Setters.Add(new Setter(TextBlock.BackgroundProperty, Brushes.Transparent));
            callStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, ColorHelper.FromHex(_appConfig.Colors.CallFontColor)));
            callStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));

            Logger.Log("Adding CALL parameter columns in reverse order...");
            foreach (var param in reversedParams)
            {

                OptionsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = param.Key,
                    Binding = new System.Windows.Data.Binding($"CallParameters[{param.Value}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    CellStyle = CreateCellStyleHelper.CreateCellStyle(callBrush),
                    Foreground = ColorHelper.FromHex(_appConfig.Colors.CallFontColor)
                });
            }

            Logger.Log("Adding CALL code column...");
            OptionsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Call Código",
                Binding = new System.Windows.Data.Binding("CallCodigo"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellStyle = CreateCellStyleHelper.CreateCellStyle(callBrush),
                Foreground = ColorHelper.FromHex(_appConfig.Colors.CallFontColor)
            });

            Logger.Log("Adding STRIKE column...");
            var strikeStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            strikeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.TextAlignmentProperty, System.Windows.TextAlignment.Center));
            strikeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.FontWeightProperty, System.Windows.FontWeights.Bold));
            strikeStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.BackgroundProperty, strikeBrush));

            OptionsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "STRIKE",
                Binding = new System.Windows.Data.Binding("Strike"),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star),
                ElementStyle = strikeStyle,
                Foreground = ColorHelper.FromHex(_appConfig.Colors.StrikeFontColor)
            });


            // ------------------------
            // PUT SIDE
            // ------------------------
            Logger.Log("Adding PUT side columns...");
            var putStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            //putStyle.Setters.Add(new Setter(TextBlock.BackgroundProperty, putBrush));
            putStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.ForegroundProperty, ColorHelper.FromHex(_appConfig.Colors.PutFontColor)));
            putStyle.Setters.Add(new Setter(System.Windows.Controls.TextBlock.TextAlignmentProperty, System.Windows.TextAlignment.Center));

            OptionsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Put Código",
                Binding = new System.Windows.Data.Binding("PutCodigo"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                CellStyle = CreateCellStyleHelper.CreateCellStyle(putBrush),
                Foreground = ColorHelper.FromHex(_appConfig.Colors.PutFontColor)
            });

            Logger.Log("Adding PUT parameter columns...");
            foreach (var param in _appConfig.PutParametersToMonitor)
            {

                OptionsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = param.Key,
                    Binding = new System.Windows.Data.Binding($"PutParameters[{param.Value}]"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                    CellStyle = CreateCellStyleHelper.CreateCellStyle(putBrush),
                    Foreground = ColorHelper.FromHex(_appConfig.Colors.PutFontColor)
                });
            }



        }


        private async void ConnectDataLoop(object sender, RoutedEventArgs e)
        {

                LoadingProgressBar.Value = 0;
                LoadingOverlay.Visibility = Visibility.Visible;

                // Created on the UI thread → every Report(...) runs this lambda back on the UI thread,
                // even when called from inside Task.Run.
                var progress = new Progress<LoadStatus>(status =>
                {
                    LoadingProgressBar.Value = status.Percent;
                    LoadingStatusText.Text = status.Message;
                });

                try
                {
                    await RunLoadPipelineAsync(progress);
                }
                catch (Exception ex)
                {
                    Logger.Log("Load pipeline error: " + ex.Message);
                    Logger.Log("Exception: " + ex.InnerException);
                    Logger.Log("Exception: " + ex.StackTrace);

                    LoadingStatusText.Text = "Erro ao carregar: " + ex.Message;
                    await Task.Delay(2500);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
        }
        private async void ConfigureColoursButton(object sender, RoutedEventArgs e)
        {

            AppConfig draft = ConfigManager.Clone(_appConfig);
            var dialog = new ColourSettingsWindow(draft) { Owner = this };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                ConfigManager.Save(draft);
                _appConfig = draft;
                ApplySettings();
                SetColours(_appConfig);
            }
        }

        private async Task HandleRtdupdates(Dictionary<int,string> topicsToHandle)
        {

            foreach(var topic in topicsToHandle)
            {

                if(topic.Key == 2)
                {

                    _stockData.LastPrice = FieldFormatter.Format(topic.Value.ToString(), "money_br");
                    Logger.Log($"Updated stock price with value: {_stockData.LastPrice}");
                    
                    // poutting last price on chart
                    Decimal.TryParse(
                    _stockData.LastPrice,
                    NumberStyles.Any,
                    new CultureInfo("pt-BR"),
                    out decimal lastPriceDecimal);
                    _chartHandler.UpdatePriceLine(lastPriceDecimal);
                    ChartHandler.RefreshChart(WpfPlot1);
                    LabelClose.Content = _stockData.LastPrice;
                }
                else
                {
                    BackgroundOption? backgroundOption = _backgroundOptionList.FirstOrDefault(x => x.topicId == topic.Key);


                    if(backgroundOption != null)
                    {
                        Double.TryParse(topic.Value, out double topicValueDouble);

                        foreach(var cutOffDate in _appConfig.AlertControl.GetSelectedCutoffs())
                        {
                            
                            _dataBaseManager.GetOptionHistoryVariation(backgroundOption, cutOffDate.Value); // Example cutoff time

                            double Zcore = (topicValueDouble - backgroundOption.average) / Math.Sqrt(backgroundOption.variance);
                            Logger.Log($"Variance {backgroundOption.variance} Average {backgroundOption.average} at cutoff {cutOffDate.Key}: {topicValueDouble}");
                            Logger.Log($"Zcore for option {backgroundOption.optionTicker} of stock {backgroundOption.stock} at cutoff {cutOffDate.Key}: {Zcore}");

                            if (Zcore > _appConfig.AlertControl.StandardDeviation)
                            {
                                if(_appConfig.AlertControl.EnableSoundAlert)
                                {
                                    player.Play();
                                }

                                Alert _alert = new Alert
                                {
                                    Ticker = backgroundOption.optionTicker,
                                    Value = topicValueDouble,
                                    ZScore = Zcore,
                                    Time = DateTime.Now,
                                    Message = cutOffDate.Key,
                                    Average = backgroundOption.average,
                                };

                                Dispatcher.Invoke(() =>
                                _alerts.Add(_alert));

                                _dataBaseManager.SaveTriggeredAlert(_alert, backgroundOption.optionTicker, backgroundOption.stock);

                                if(_appConfig.AlertControl.EnableSpeechAlert)
                                {
                                    _speechService.SpeakAsync($"Alerta de movimentação acima da média do da opção {backgroundOption.optionTicker} do ativo {backgroundOption.stock} no intervalo de {cutOffDate.Key}");
                                }
                            }
                        }

                        await _dataBaseManager.SaveOptionHistory(backgroundOption);
                    }

                }

            }
        }

        private void HideAlertWindow()
        {
            if (_alertWindow != null && _alertWindow.IsVisible)
            {
                _alertWindow.Hide();
            }
        }

        private async Task loadDataAsync()
        {
            //RTDClient RtdClientObj = new RTDClient();
            _appConfig = ConfigManager.Load();
            ConfigManager.Validate(_appConfig);
            Logger.configureLogging(_appConfig.debugSteps, _appConfig);
            Logger.Log("Configuration loaded successfully.");
            Logger.Log("Configuration validated successfully. Ticker: " + _appConfig.Ticker);

            TickerInput.Text = _appConfig.Ticker;

            Logger.Log("Inicializando Banco de dados" + _appConfig.Ticker);
            _dataBaseManager.Initialize(_appConfig.DatabasePath);
            
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _rtdClient?.StopAsync();
            Logger.Log("RTD server disposed");
            StopLoops();
        }
        private async Task RunLoadPipelineAsync(IProgress<LoadStatus> progress)
        {
            if (loopingIsRunning) { 
                Logger.Log("Stopping loops before reloading data...");
                progress.Report(new LoadStatus(25, "Parando loops..."));
                StopLoops();
                ConnectionStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                Status.Text = "RTD Desconectado";
                Status.Foreground = new SolidColorBrush(Colors.Red);
                loopingIsRunning = false;
                ConnectDataLoopButtonTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                ConnectDataLoopButton.Background = new SolidColorBrush(Colors.Gray);
                progress.Report(new LoadStatus(100, "Parada Excutada"));
                return;
            }

            Logger.Log("Starting load pipeline for ticker: " + _appConfig.Ticker);

            // 1) Ensure the RTD server is up
            progress.Report(new LoadStatus(10, "Iniciando servidor RTD..."));
            if (_rtdClient == null) _rtdClient = new RTDClient();
            if (!_rtdClient.IsRtdConnected) await _rtdClient.InitializeRtdServerAsync();
            if (!_rtdClient.IsRtdConnected)
                throw new InvalidOperationException("Não foi possível iniciar o servidor RTD.");

            // 2) Download the option chain (network I/O)
            progress.Report(new LoadStatus(20, "Baixando cadeia de opções..."));
            var data = await Web_Scrapper.GetData($"{_appConfig.BaseUrl}{_appConfig.Ticker}");

            progress.Report(new LoadStatus(30, "Salvando opções no banco de dados"));

            _dataBaseManager.SoftDeleteOptions(_appConfig.Ticker);

            var callOptions = await OptionMapper.extractCallDataFromHTMLAsync(data, _appConfig.CallOptionTableOnWeb, _dataBaseManager);
            var putOptions = await OptionMapper.extractPutDataFromHTMLAsync(data, _appConfig.PutOptionTableOnWeb, _dataBaseManager);

            // to perform a performatic softdelete properly, I'll delete all the ones that are active, and during the creation I activate the ones that are still valid again
            if (callOptions.Count == 0 || putOptions.Count == 0)
            {
                Logger.Log("Failed to populate option object.");
                string lastSuccessfullWebScrapping = _dataBaseManager.getLastSuccessfullWebScrapping(_appConfig.Ticker);
                alertMark.HelpText = alertMark.HelpText + "Falha ao baixar a cadeia de opções. Usando dados do banco de dados local. &#x0a; Ultima requisição - Ativo : Data &#x0a; -> " + _appConfig.Ticker + lastSuccessfullWebScrapping;
                alertMark.Visibility = System.Windows.Visibility.Visible;
                callOptions = _dataBaseManager.GetoptionOndDatabaseData(_appConfig.Ticker, true);
                putOptions = _dataBaseManager.GetoptionOndDatabaseData(_appConfig.Ticker, false);
            }
            else
            {
                _dataBaseManager.saveLastSuccessfullWebScrapping(_appConfig.Ticker, DateTime.Now);
            }

            progress.Report(new LoadStatus(40, "Construindo colunas dinâmicas..."));
            BuildDynamicColumns();

            // 3) Build grid rows (CPU) on a background thread
            progress.Report(new LoadStatus(50, "Montando as linhas de opções..."));
            var rows = await Task.Run(() => OptionMapper.BuildOptionRows(callOptions,putOptions));

            // 4) Connect topics (heavy COM) on a background thread — this is what used to freeze
            progress.Report(new LoadStatus(60, "Conectando dados ao RTD..."));
            _chartLookup.Clear();
            await Task.Run(() => _rtdClient.ConnectDataToRTd(rows, _appConfig, _chartLookup, _stockData, _backgroundOptionList));

            // 5) Fill the UI — we are back on the UI thread after the await, so this is safe
            progress.Report(new LoadStatus(65, "Preenchendo a grade e o gráfico..."));
            _optionsMonitored.Clear();
            foreach (var row in rows) _optionsMonitored.Add(row);
            _chartRows.Clear();
            foreach (var chartRow in _chartLookup.Values) _chartRows.Add(chartRow);

            OptionsGrid.ItemsSource = _optionsMonitored;
            LabelClose.Content = _stockData.LastPrice;
            LabelOpening.Content = _stockData.OpeningPrice;

            _ = _chartHandler.BuildChart(_chartLookup, _appConfig, WpfPlot1, _stockData);
            
            _orderedChartRows = _chartLookup.OrderBy(x => x.Key).Select(x => x.Value).ToList(); ;

            _hoverCrosshair = WpfPlot1.Plot.Add.Crosshair(0, 0);
            _hoverCrosshair.IsVisible = false;          // hidden until the mouse is over a bar
            _hoverCrosshair.MarkerShape = ScottPlot.MarkerShape.OpenCircle;
            _hoverCrosshair.MarkerSize = 15;
            WpfPlot1.MouseMove += WpfPlot1_MouseMove;
            // 6) Start the live loops
            ConnectionStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
            Status.Text = "RTD Conectado";
            Status.Foreground = new SolidColorBrush(Colors.Green);

            // 7) connect extra topics that need to be monitored in background
            
            
            progress.Report(new LoadStatus(70, "Iniciando atualizações em tempo real..."));
            Logger.Log("Loading background options for real-time updates...");

            List<BackgroundOption> tempOptionList = new List<BackgroundOption>();

            int tempCount = 1;

             foreach (var stock in _appConfig.AlertControl.StocksToMonitor)
            {
                List<BackgroundOption> auxList = new List<BackgroundOption>();
                progress.Report(new LoadStatus(70, "Conectando ativo " + stock.Symbol));
                Logger.Log($"Loading background options for stock: {stock}");
                auxList = await ProgramInterface.MapsBackgroundOptions(stock.Symbol, _appConfig,_dataBaseManager);
                if(auxList != null && auxList.Count > 0)
                {
                    _dataBaseManager.saveLastSuccessfullWebScrapping(stock.Symbol, DateTime.Now);
                    tempOptionList.AddRange(auxList);
                }
                else
                {
                    _dataBaseManager.saveLastSuccessfullWebScrapping(stock.Symbol, DateTime.Now);
                    alertMark.HelpText = alertMark.HelpText + "&#x0a; -> "+stock.Symbol + " : " + _dataBaseManager.getLastSuccessfullWebScrapping(stock.Symbol);
                    Logger.Log($"No background options found for stock: {stock}");
                }

            }

            Logger.Log("Connecting background options...");

            progress.Report(new LoadStatus(72, $"Conectando dados ao RTD 0/{tempOptionList.Count}"));
            tempCount = 1;
            foreach (var option in tempOptionList)
            {
                await _rtdClient.ConnectBackgroundOptions(_appConfig, option);

            }

            Logger.Log("Background options connected.");
            _backgroundOptionList.AddRange(tempOptionList);

            Logger.Log("Saving background options on Data Base");
            progress.Report(new LoadStatus(80, $"Atualizando Series historicas 0/{_backgroundOptionList.Count}"));

            tempCount = 1;

            foreach (var option in _backgroundOptionList)
            {
                await _dataBaseManager.SaveOptionHistory(option);
               _dataBaseManager.GetOptionHistoryVariation(option, System.DateTime.Parse(_appConfig.AlertControl.StartDate, CultureInfo.InvariantCulture)); // Example cutoff time
            }

            // 8) Start the loops for RTD and Scraper
            progress.Report(new LoadStatus(90, "Iniciando atualizações em tempo real..."));
            StartLoops();
            progress.Report(new LoadStatus(100, "Servidor conectado"));
        }
        private async void ServerStart_click(object sender, RoutedEventArgs e)
        {
            // temporary empty handler
            Logger.Log("Starting RTD server...");
            if (_rtdClient == null)
            {
                Logger.Log("RTD client instance is null. Creating new instance...");
                _rtdClient = new RTDClient();
            }

            if (!_rtdClient.IsRtdConnected)
            {
                Logger.Log("RTD server is currently inactive. Activating...");
                await _rtdClient.InitializeRtdServerAsync();
                ConnectServerButtonTextBlock.Text = "\uE704";
            }
            else
            {
                Logger.Log("RTD server is currently active. Deactivating...");
                await _rtdClient.StopAsync();
                ConnectServerButtonTextBlock.Text = "\uE7E8";
                StopLoops();
            }


        }

        private void SetColours(AppConfig _appConfig)
        {
            LabelClose.Foreground = ColorHelper.FromHex(_appConfig.Colors.ClosePrice);
            LabelOpening.Foreground = ColorHelper.FromHex(_appConfig.Colors.OpenPrice);
            StrikeSelected.Foreground = ColorHelper.FromHex(_appConfig.Colors.StrikeFontColorBar);
            PutValue.Foreground = ColorHelper.FromHex(_appConfig.Colors.PutFontColorBar);
            CallValue.Foreground = ColorHelper.FromHex(_appConfig.Colors.CallFontColorBar);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            AppConfig draft = ConfigManager.Clone(_appConfig);
            var dialog = new SettingsWindow(draft) { Owner = this };

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                ConfigManager.Save(draft);
                _appConfig = draft;
                ApplySettings();
            }
        }

        private void ShowAlertWindow()
        {
            if (_alertWindow == null)
            {
                _alertWindow = new AlertWindow(_alerts);

                _alertWindow.Closed += (sender, e) =>
                {
                    _alertWindow = null;
                };
            }

            if (!_alertWindow.IsVisible)
            {
                _alertWindow.Show();
            }

            _alertWindow.Activate();
        }

        private async void StartLoops()
        {

            _ = StartRtdLoop();
            _ = StartScraperLoop();
            loopingIsRunning = true;
        }

        private async Task StartRtdLoop()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (_rtdClient != null && _rtdClient.IsRtdConnected)
                    {
                        

                        Dictionary<int, string> topicsReturned = new Dictionary<int, string>();

                        if (_appConfig.generatesRandomData == true)
                        {
                            Random _random = new Random();

                            topicsReturned = new Dictionary<int, string>
                            {
                                { _random.Next(0, 5000), $"{_random.Next(1, 5000)}" }
                            };
                        }
                        else
                        {
                            topicsReturned = _rtdClient.UpdateRtdData(_appConfig);

                        }

                        Logger.Log($"RTD loop fetched {topicsReturned.Count} topics.");
                        Logger.Log($"Topics returned: {string.Join(", ", topicsReturned.Select(kvp => $"[{kvp.Key}: {kvp.Value}]"))}");
                        if (topicsReturned != null && topicsReturned.Count > 0 )
                        {
                            _ = HandleRtdupdates(topicsReturned);
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logger.Log("RTD loop error: " + ex.Message);
                }

                await Task.Delay(_appConfig.RefreshIntervalMilliseconds, _cts.Token);
            }
        }

        private async Task StartScraperLoop()
        {

            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(_appConfig.ScraperIntervalMinutes), _cts.Token);
                
                try
                {
                    LoadingProgressBar.Value = 0;
                    LoadingOverlay.Visibility = Visibility.Visible;

                    // Created on the UI thread → every Report(...) runs this lambda back on the UI thread,
                    // even when called from inside Task.Run.
                    var progress = new Progress<LoadStatus>(status =>
                    {
                        LoadingProgressBar.Value = status.Percent;
                        LoadingStatusText.Text = status.Message;
                    });

                    await RunLoadPipelineAsync(progress);
                    LoadingOverlay.Visibility = Visibility.Hidden;
                    Logger.Log("Refreshing options from website...");

                }
                catch (Exception ex)
                {
                    Logger.Log("Scraper loop error: " + ex.Message);
                }

            }
        }

        private void StopLoops()
        {
            _cts.Cancel();
        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await loadDataAsync();
            _alerts.CollectionChanged += Alerts_CollectionChanged;
            DataContext = this;
        }

        private void WpfPlot1_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_orderedChartRows.Count == 0) return;

            // (a) WPF mouse position -> ScottPlot pixel. DisplayScale is the WPF-specific bit.
            System.Windows.Point p = e.GetPosition(WpfPlot1);
            ScottPlot.Pixel mousePixel = new(p.X * WpfPlot1.DisplayScale, p.Y * WpfPlot1.DisplayScale);

            // (b) pixel -> chart coordinates
            ScottPlot.Coordinates mouse = WpfPlot1.Plot.GetCoordinates(mousePixel);

            // (c) bars sit at integer X (0,1,2,...), so the nearest bar is the rounded X
            int index = (int)System.Math.Round(mouse.X);

            // (d) mouse is off any bar -> hide and bail
            if (index < 0 || index >= _orderedChartRows.Count)
            {
                if (_hoverCrosshair.IsVisible)
                {
                    _hoverCrosshair.IsVisible = false;
                    StrikeSelected.Content = "";
                    PutValue.Content = "";
                    CallValue.Content = "";
                    WpfPlot1.Refresh();
                    _lastHoverIndex = -1;
                }
                return;
            }

            // (e) PERFORMANCE GUARD: MouseMove fires constantly. Only redraw when the
            //     bar under the cursor actually changes, not on every pixel of movement.
            if (index == _lastHoverIndex) return;
            _lastHoverIndex = index;

            ChartRow row = _orderedChartRows[index];
            _hoverCrosshair.IsVisible = true;
            _hoverCrosshair.Position = new ScottPlot.Coordinates(index, mouse.Y);
            StrikeSelected.Content = $"R$ {row.Strike}";
            PutValue.Content = $"{row.PutValue:0.##}";
            CallValue.Content = $"{row.CallValue:0.##}";
            WpfPlot1.Refresh();
        }

    }


}