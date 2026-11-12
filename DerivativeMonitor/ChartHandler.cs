using ScottPlot;
using ScottPlot.LayoutEngines;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Globalization;
public class ChartHandler
{
    public VerticalLine? PriceLine { get; private set; }

    private List<ChartRow>? InternalLookUp; 

    public async Task BuildChart(Dictionary<decimal, ChartRow> chartLookup, AppConfig _appConfig, WpfPlot wpfPlot, StockData stockData)
    {
        wpfPlot.Plot.Clear();
        string[] categorieNames = { "Put", "Call" };
        string[] categoryColours = { _appConfig.Colors.ChartPut, _appConfig.Colors.ChartCall };

        InternalLookUp = chartLookup
                .OrderBy(x => x.Key)
                .Select(x => x.Value)
                .ToList();

        double[] putValues = [.. chartLookup.Values.Select(row => row.PutValue)];
        double[] callValues = [.. chartLookup.Values.Select(row => row.CallValue)];

        var putSeries = wpfPlot.Plot.Add.Bars(putValues);
        putSeries.LegendText = "Put";

        var callSeries = wpfPlot.Plot.Add.Bars(callValues);
        callSeries.LegendText = "Call";
        ScottPlot.TickGenerators.NumericManual tickGen = new();

        Decimal.TryParse(
                stockData.LastPrice,
                NumberStyles.Any,
                new CultureInfo("pt-BR"),
                out decimal lastPriceDecimal);
        
        Logger.Log("orded rows:" + InternalLookUp.Count);

        for (int i = 0; i < InternalLookUp.Count -1 ; i++)
        {

            var current = InternalLookUp[i];
            var next = InternalLookUp[i + 1];

            //var chartRow = chartLookup.ElementAt(i).Value;
            tickGen.AddMajor(i, FieldFormatter.Format(current.Strike.ToString(), _appConfig.Chart.FieldFormatChart));

            if (current.Strike <= lastPriceDecimal &&
                 next.Strike >= lastPriceDecimal)
            {

                decimal range = next.Strike - current.Strike;

                double position = i;

                if (range > 0)
                {
                    position += (double)
                        ((lastPriceDecimal - current.Strike) / range);
                }
                Logger.Log($"Price line position: {position}");
                tickGen.AddMajor(
                        position,
                        FieldFormatter.Format(lastPriceDecimal.ToString(), _appConfig.Chart.FieldFormatChart));

                PriceLine = wpfPlot.Plot.Add.VerticalLine(position);

                PriceLine.Text = $"{stockData.LastPrice}";
                PriceLine.Color = new   ScottPlot.Color(_appConfig.Colors.ChartActualPrice);
                PriceLine.LabelAlignment = Alignment.MiddleRight;

            }
        }
       
        putSeries.Color = new ScottPlot.Color(_appConfig.Colors.ChartPut);
        callSeries.Color = new ScottPlot.Color(_appConfig.Colors.ChartCall);
        wpfPlot.Plot.Axes.Bottom.TickGenerator = tickGen;
        wpfPlot.Plot.XLabel(_appConfig.Chart.XAxisTitle);
        wpfPlot.Plot.YLabel(_appConfig.Chart.YAxisTitle);

        wpfPlot.Plot.ShowLegend(Alignment.UpperLeft);
        wpfPlot.Plot.Axes.Margins(bottom: 0);

        var yAxis = wpfPlot.Plot.Axes.Left;

        yAxis.TickGenerator =
            new ScottPlot.TickGenerators.NumericFixedInterval(
                _appConfig.Chart.ChartWidth);

        RefreshChart(wpfPlot);
    }
    public static void RefreshChart(WpfPlot wpfPlot)
    {
        wpfPlot.Refresh();
    }

    private double GetPricePosition(decimal price)
    {

        for (int i = 0; i < InternalLookUp.Count - 1; i++)
        {
            var current = InternalLookUp[i];
            var next = InternalLookUp[i + 1];

            if (current.Strike <= price &&
                next.Strike >= price)
            {
                decimal range =
                    next.Strike - current.Strike;

                return i +
                    (double)((price - current.Strike) / range);
            }
        }

        return 0;
    }

    public void UpdatePriceLine(
    decimal price)
    {
        if (PriceLine == null)
            return;

        double position =
            GetPricePosition(price);

        PriceLine.X = position;
        PriceLine.Text = $"{price}";
    }


}
