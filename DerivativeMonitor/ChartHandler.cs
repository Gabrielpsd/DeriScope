using ScottPlot;
using ScottPlot.LayoutEngines;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using System.Globalization;
public static class ChartHandler
{
    public static void BuildChart(Dictionary<decimal, ChartRow> chartLookup, AppConfig _appConfig, WpfPlot wpfPlot, StockData stockData , ref VerticalLine priceLine)
    {
        wpfPlot.Plot.Clear();
        string[] categorieNames = { "Put", "Call" };
        string[] categoryColours = { _appConfig.Colors.ChartPut, _appConfig.Colors.ChartCall };
        double[] putValues = chartLookup.Values.Select(row => row.PutValue).ToArray();
        double[] callValues = chartLookup.Values.Select(row => row.CallValue).ToArray();

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

        var orderedRows = chartLookup
                .OrderBy(x => x.Key)
                .Select(x => x.Value)
                .ToList();
        
        Logger.Log("orded rows:" + orderedRows.Count);

        for (int i = 0; i < orderedRows.Count -1; i++)
        {

            var current = orderedRows[i];
            var next = orderedRows[i + 1];

            //var chartRow = chartLookup.ElementAt(i).Value;

            tickGen.AddMajor(i, FieldFormatter.Format(current.Strike.ToString(), _appConfig.Chart.FieldFormatChart));

            if (current.Strike <= lastPriceDecimal &&
                 next.Strike >= lastPriceDecimal)
            {
                if (priceLine == null)
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

                    priceLine = wpfPlot.Plot.Add.VerticalLine(position);

                    priceLine.Text = $"{stockData.LastPrice}";
                    priceLine.Color = new   ScottPlot.Color(_appConfig.Colors.ChartActualPrice);
                    priceLine.LabelAlignment = Alignment.MiddleRight;
                }
            }
        }
       
        putSeries.Color = new ScottPlot.Color(_appConfig.Colors.ChartPut);
        callSeries.Color = new ScottPlot.Color(_appConfig.Colors.ChartCall);
        wpfPlot.Plot.Axes.Bottom.TickGenerator = tickGen;
        wpfPlot.Plot.XLabel(_appConfig.Chart.XAxisTitle);
        wpfPlot.Plot.YLabel(_appConfig.Chart.YAxisTitle);

        wpfPlot.Plot.ShowLegend(Alignment.UpperLeft);
        wpfPlot.Plot.Axes.Margins(bottom: 0);
        var limits = wpfPlot.Plot.Axes.GetLimits();
        wpfPlot.Plot.Axes.SetLimitsY(0, top: limits.Top * 1.05);
        RefreshChart(wpfPlot);
    }
    public static void RefreshChart(WpfPlot wpfPlot)
    {
        wpfPlot.Refresh();
    }

    private static double GetPricePosition(Dictionary<decimal, ChartRow> chartLookup, decimal price)
    {
        var rows = chartLookup
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();

        for (int i = 0; i < rows.Count - 1; i++)
        {
            var current = rows[i];
            var next = rows[i + 1];

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

    public static void UpdatePriceLine(
    decimal price,
    Dictionary<decimal, ChartRow> chartLookup, ref VerticalLine priceLine)
    {
        if (priceLine == null)
            return;

        double position =
            GetPricePosition(chartLookup, price);

        priceLine.X = position;
        priceLine.Text = $"{price}";
    }


}
