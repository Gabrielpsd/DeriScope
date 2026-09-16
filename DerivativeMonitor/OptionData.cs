using System.Collections.ObjectModel;
using System.ComponentModel;

/* This object represents the data structure for an option that comes from the B3 website, 
 * including properties such as ISIN, specification, code, 
 * expiration date, exercise price, reference, protection status, 
 * and style. It implements the INotifyPropertyChanged interface to allow for property change notifications, 
 * which is useful for data binding in WPF applications. Not all the information is used in the program, but is coming from the request anyway
 * so for now is being maintened in the class. */
public class OptionData
{
    public string? Codigo { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal PrecoExercicio { get; set; }

}

