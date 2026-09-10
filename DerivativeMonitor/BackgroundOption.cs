using System;

public class BackgroundOption
{
	public string stock { get; set; } = "";

	public string optionTicker { get; set; } = "";

	public string parameter { get; set; } = "";

	public decimal value { get; set; } = 0;

	public int topicId { get; set; } = 0;

	public double average { get; set; } = 0;

    public double variance { get; set; } = 0;
}
