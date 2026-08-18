using System;

namespace HansLaserDateSerialDemo
{
    public class MarkingRecord
    {
        int id { get; set; }
        string code { get; set; }
        DateTime timestamp { get; set; }
        Product product { get; set; }
    }
}