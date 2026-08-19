using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HansLaserDateSerialDemo
{
    internal static class MarkingRecordStates
    {
        public const string Pending = "Pending";
        public const string Marked = "Marked";
        public const string Skipped = "Skipped";
        public const string Reprinted = "Reprinted";
    }

    internal sealed class Reservation
    {
        public int RecordId { get; set; }
        public int ProductId { get; set; }
        public DateTime Date { get; set; }
        public int Serial { get; set; }
        public string Code { get; set; }
        public bool WasAlreadyPending { get; set; }
    }

    internal sealed class SequenceStore
    {
        private readonly int _productId;
        private readonly int _serialStartValue;
        private readonly ICodeGenerator _codeGenerator;
        private readonly object _gate = new object();

        public SequenceStore(Product product, ICodeGenerator codeGenerator)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (product.Id <= 0)
                throw new ArgumentException("产品必须先保存到数据库。", nameof(product));
            if (codeGenerator == null)
                throw new ArgumentNullException(nameof(codeGenerator));

            _productId = product.Id;
            _serialStartValue = NormalizeSerial(product.SerialStartValue);
            _codeGenerator = codeGenerator;
        }

        public Reservation GetOrReserve(DateTime now)
        {
            lock (_gate)
            {
                using (AppDbContext dbContext = new AppDbContext())
                {
                    dbContext.EnsureDatabase();
                    using (var transaction = dbContext.Database.BeginTransaction())
                    {
                        DateTime today = now.Date;
                        ProductSequenceState state = LoadOrCreateState(dbContext, today);

                        if (state.PendingRecordId.HasValue)
                        {
                            MarkingRecord pending = dbContext.MarkingRecords
                                .Single(record => record.Id == state.PendingRecordId.Value);
                            transaction.Commit();
                            return ToReservation(pending, true);
                        }

                        if (state.BusinessDate.Date != today)
                        {
                            state.BusinessDate = today;
                            state.NextSerial = _serialStartValue;
                        }

                        if (state.NextSerial < 1 || state.NextSerial > 9999)
                            throw new InvalidOperationException("当天流水号已经达到 9999，禁止回卷到起始值。");

                        int serial = state.NextSerial;
                        string code = _codeGenerator.Build(today, serial);
                        MarkingRecord record = new MarkingRecord
                        {
                            ProductId = _productId,
                            Code = code,
                            Serial = serial,
                            BusinessDate = today,
                            State = MarkingRecordStates.Pending,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        dbContext.MarkingRecords.Add(record);
                        try
                        {
                            dbContext.SaveChanges();
                        }
                        catch (DbUpdateException ex) when (IsUniqueCodeViolation(ex))
                        {
                            throw new InvalidOperationException(
                                $"编号已存在，无法占用新编号：{code}。请检查产品编码前缀、日期和流水号起始值。", ex);
                        }

                        state.PendingRecordId = record.Id;
                        state.NextSerial = serial + 1;
                        dbContext.SaveChanges();
                        transaction.Commit();

                        return ToReservation(record, false);
                    }
                }
            }
        }

        public void Complete(string code)
        {
            ResolvePending(code, MarkingRecordStates.Marked, "打标正常完成");
        }

        public void SkipOrConfirmAlreadyMarked(string code)
        {
            ResolvePending(code, MarkingRecordStates.Skipped, "操作员确认已用或跳过");
        }

        private void ResolvePending(string code, string stateValue, string remark)
        {
            lock (_gate)
            {
                using (AppDbContext dbContext = new AppDbContext())
                {
                    dbContext.EnsureDatabase();
                    using (var transaction = dbContext.Database.BeginTransaction())
                    {
                        ProductSequenceState state = dbContext.ProductSequenceStates
                            .SingleOrDefault(item => item.ProductId == _productId);
                        if (state == null || !state.PendingRecordId.HasValue)
                            throw new InvalidOperationException("当前没有待确认编号。");

                        MarkingRecord record = dbContext.MarkingRecords
                            .Single(item => item.Id == state.PendingRecordId.Value);
                        if (!string.Equals(record.Code, code, StringComparison.Ordinal))
                            throw new InvalidOperationException($"待确认编号不一致。数据库：{record.Code}，程序：{code}");

                        DateTime now = DateTime.Now;
                        record.State = stateValue;
                        record.UpdatedAt = now;
                        record.Remark = remark;
                        if (string.Equals(stateValue, MarkingRecordStates.Marked, StringComparison.Ordinal))
                            record.MarkedAt = now;

                        state.PendingRecordId = null;
                        dbContext.SaveChanges();
                        transaction.Commit();
                    }
                }
            }
        }

        private ProductSequenceState LoadOrCreateState(AppDbContext dbContext, DateTime today)
        {
            ProductSequenceState state = dbContext.ProductSequenceStates
                .SingleOrDefault(item => item.ProductId == _productId);
            if (state != null)
                return state;

            state = new ProductSequenceState
            {
                ProductId = _productId,
                BusinessDate = today,
                NextSerial = _serialStartValue
            };
            dbContext.ProductSequenceStates.Add(state);
            dbContext.SaveChanges();
            return state;
        }

        private static Reservation ToReservation(MarkingRecord record, bool wasAlreadyPending)
        {
            return new Reservation
            {
                RecordId = record.Id,
                ProductId = record.ProductId,
                Date = record.BusinessDate.Date,
                Serial = record.Serial,
                Code = record.Code,
                WasAlreadyPending = wasAlreadyPending
            };
        }

        private static int NormalizeSerial(int value)
        {
            if (value < 1)
                return 1;
            if (value > 9999)
                return 9999;
            return value;
        }

        private static bool IsUniqueCodeViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqliteException sqliteException &&
                   sqliteException.SqliteErrorCode == 19 &&
                   sqliteException.Message.Contains("MarkingRecords.Code", StringComparison.OrdinalIgnoreCase);
        }
    }
}
