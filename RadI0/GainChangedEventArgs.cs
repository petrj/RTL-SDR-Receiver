namespace RadI0;

public class GainChangedEventArgs : EventArgs
{
    public bool HWGain {get;set;}
    public bool SWGain {get;set;}
    public int ManualGainValue {get;set;}
}
