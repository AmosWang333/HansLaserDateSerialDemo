namespace HansLaserDateSerialDemo;

public class Selection<T>
{
    public string Label { get; set; }
    public T Value { get; set; }

    public Selection(string label, T value)
    {
        Label = label;
        Value = value;
    }

    public override string ToString()
    {
        return Label;
    }
}
