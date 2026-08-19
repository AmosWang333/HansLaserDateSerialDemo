using System;
using System.ComponentModel.DataAnnotations;

namespace HansLaserDateSerialDemo
{
    public class MarkingRecord
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public int Serial { get; set; }
        public DateTime BusinessDate { get; set; }
        public string State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? MarkedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Remark { get; set; }
        public int? SourceRecordId { get; set; }

        // 外键属性
        public int ProductId { get; set; }

        // 导航属性
        public Product Product { get; set; }
        public MarkingRecord SourceRecord { get; set; }
    }
}
