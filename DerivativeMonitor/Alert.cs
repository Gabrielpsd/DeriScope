using System;
using System.Collections.Specialized;

public class Alert
{
    public string Ticker { get; set; }
    public double Value { get; set; }

    public double Average { get; set; } = 0;
    public double ZScore { get; set; }
    public DateTime Time { get; set; }
    public string Message { get; set; }

}

