using System;

namespace HansLaserDateSerialDemo
{
    public class ProductSequenceState
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public DateTime BusinessDate { get; set; }
        public int NextSerial { get; set; }
        public int? PendingRecordId { get; set; }

        public Product Product { get; set; }
        public MarkingRecord PendingRecord { get; set; }
    }
}
