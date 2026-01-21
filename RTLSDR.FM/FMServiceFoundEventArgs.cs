namespace RTLSDR.FM;

public class FMServiceFoundEventArgs: EventArgs
{
    public int Frequency { get;set; }
    public double Percents { get;set; }
}