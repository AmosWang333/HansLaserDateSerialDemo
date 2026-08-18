using System;
using System.ComponentModel.DataAnnotations;

namespace HansLaserDateSerialDemo
{
    public class MarkingRecord
    {
        public int Id { get; set; }
        public string Code { get; set; }

        public DateTime Timestamp { get; set; }

        // 外键属性
        public int ProductId { get; set; }

        // 导航属性
        public Product Product { get; set; }
    }
}