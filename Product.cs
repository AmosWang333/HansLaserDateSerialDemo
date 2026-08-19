using System.ComponentModel.DataAnnotations;

namespace HansLaserDateSerialDemo
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CustomerPartNumber { get; set; }
        public int Shipcode { get; set; }
        public string TemplatePath { get; set; }
        public string Pattern { get; set; }
        public int SerialStartValue { get; set; } = 1;
    }
}
