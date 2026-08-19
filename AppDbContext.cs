using Microsoft.EntityFrameworkCore;

namespace HansLaserDateSerialDemo;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<MarkingRecord> MarkingRecords { get; set; }
    public DbSet<ProductSequenceState> ProductSequenceStates { get; set; }

    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.db");
    private static readonly object SchemaGate = new object();
    private static bool _schemaEnsured;

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductSequenceState>()
            .HasIndex(state => state.ProductId)
            .IsUnique();

        modelBuilder.Entity<ProductSequenceState>()
            .HasOne(state => state.Product)
            .WithMany()
            .HasForeignKey(state => state.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductSequenceState>()
            .HasOne(state => state.PendingRecord)
            .WithMany()
            .HasForeignKey(state => state.PendingRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MarkingRecord>()
            .HasIndex(record => record.Code)
            .IsUnique();

        modelBuilder.Entity<MarkingRecord>()
            .HasOne(record => record.Product)
            .WithMany()
            .HasForeignKey(record => record.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MarkingRecord>()
            .HasOne(record => record.SourceRecord)
            .WithMany()
            .HasForeignKey(record => record.SourceRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void EnsureDatabase()
    {
        lock (SchemaGate)
        {
            if (_schemaEnsured)
                return;

            Database.EnsureCreated();
            EnsureTables();
            EnsureColumn("Products", "CodeGeneratorType", "TEXT NOT NULL DEFAULT 'EcoFlow'");
            EnsureColumn("Products", "SerialStartValue", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumn("MarkingRecords", "Serial", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn("MarkingRecords", "BusinessDate", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
            EnsureColumn("MarkingRecords", "CreatedAt", "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'");
            EnsureColumn("MarkingRecords", "MarkedAt", "TEXT NULL");
            EnsureColumn("MarkingRecords", "UpdatedAt", "TEXT NULL");
            EnsureColumn("MarkingRecords", "Remark", "TEXT NULL");
            EnsureColumn("MarkingRecords", "SourceRecordId", "INTEGER NULL");

            EnsureIndexes();
            _schemaEnsured = true;
        }
    }

    private void EnsureTables()
    {
        Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER NOT NULL CONSTRAINT PK_Products PRIMARY KEY AUTOINCREMENT,
                Name TEXT NULL,
                CustomerPartNumber TEXT NULL,
                Shipcode INTEGER NOT NULL,
                TemplatePath TEXT NULL,
                Pattern TEXT NULL,
                CodeGeneratorType TEXT NOT NULL DEFAULT 'EcoFlow',
                SerialStartValue INTEGER NOT NULL DEFAULT 1
            )");

        Database.ExecuteSqlRaw(
            @"CREATE TABLE IF NOT EXISTS MarkingRecords (
                Id INTEGER NOT NULL CONSTRAINT PK_MarkingRecords PRIMARY KEY AUTOINCREMENT,
                Code TEXT NULL,
                Serial INTEGER NOT NULL DEFAULT 0,
                BusinessDate TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                State TEXT NULL,
                CreatedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                MarkedAt TEXT NULL,
                UpdatedAt TEXT NULL,
                Remark TEXT NULL,
                SourceRecordId INTEGER NULL,
                ProductId INTEGER NOT NULL,
                CONSTRAINT FK_MarkingRecords_Products_ProductId
                    FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
                CONSTRAINT FK_MarkingRecords_MarkingRecords_SourceRecordId
                    FOREIGN KEY (SourceRecordId) REFERENCES MarkingRecords (Id) ON DELETE RESTRICT
            )");

            Database.ExecuteSqlRaw(
                @"CREATE TABLE IF NOT EXISTS ProductSequenceStates (
                    Id INTEGER NOT NULL CONSTRAINT PK_ProductSequenceStates PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL,
                    BusinessDate TEXT NOT NULL,
                    NextSerial INTEGER NOT NULL,
                    PendingRecordId INTEGER NULL,
                    CONSTRAINT FK_ProductSequenceStates_Products_ProductId
                        FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ProductSequenceStates_MarkingRecords_PendingRecordId
                        FOREIGN KEY (PendingRecordId) REFERENCES MarkingRecords (Id) ON DELETE SET NULL
                )");
    }

    private void EnsureIndexes()
    {
        Database.ExecuteSqlRaw(
            @"CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductSequenceStates_ProductId
                ON ProductSequenceStates (ProductId)");
        Database.ExecuteSqlRaw(
            @"DROP INDEX IF EXISTS IX_MarkingRecords_ProductId_Code");
        EnsureNoDuplicateMarkingRecordCodes();
        Database.ExecuteSqlRaw(
            @"CREATE UNIQUE INDEX IF NOT EXISTS IX_MarkingRecords_Code
                ON MarkingRecords (Code)");
        Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_MarkingRecords_ProductId
                ON MarkingRecords (ProductId)");
        Database.ExecuteSqlRaw(
            @"CREATE INDEX IF NOT EXISTS IX_ProductSequenceStates_PendingRecordId
                ON ProductSequenceStates (PendingRecordId)");
    }

    private void EnsureNoDuplicateMarkingRecordCodes()
    {
        string duplicateCode = Database.SqlQueryRaw<string>(
                @"SELECT Code
                  FROM MarkingRecords
                  WHERE Code IS NOT NULL
                  GROUP BY Code
                  HAVING COUNT(*) > 1
                  LIMIT 1")
            .AsEnumerable()
            .FirstOrDefault();

        if (duplicateCode != null)
            throw new InvalidOperationException($"MarkingRecords.Code 存在重复值，无法创建唯一索引：{duplicateCode}");
    }

    private void EnsureColumn(string tableName, string columnName, string columnDefinition)
    {
        try
        {
#pragma warning disable EF1002
            // Identifiers are hard-coded by callers; SQLite cannot parameterize ALTER TABLE identifiers.
            Database.ExecuteSqlRaw($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
#pragma warning restore EF1002
        }
        catch
        {
            // SQLite has no ADD COLUMN IF NOT EXISTS on older runtimes; duplicate column means the schema is already ready.
        }
    }
}

public class ProductService(AppDbContext dbContext)
{
    public void AddProduct(Product product)
    {
        dbContext.EnsureDatabase();
        dbContext.Products.Add(product);
        dbContext.SaveChanges();
    }

    public Product GetProduct(int id)
    {
        dbContext.EnsureDatabase();
        return dbContext.Products.Find(id);
    }

    public List<Product> GetProducts()
    {
        dbContext.EnsureDatabase();
        return dbContext.Products.OrderBy(product => product.Name).ThenBy(product => product.CustomerPartNumber)
            .ToList();
    }

    public List<Selection<Product>> GetProductSelection()
    {
        var selections = new List<Selection<Product>>();
        foreach (Product product in GetProducts())
        {
            selections.Add(new Selection<Product>($"{product.Name}[{product.CustomerPartNumber}]", product));
        }

        return selections;
    }

    public void UpdateProduct(Product product)
    {
        dbContext.EnsureDatabase();
        dbContext.Entry(product).State = EntityState.Modified;
        dbContext.SaveChanges();
    }

    public void DeleteProduct(int id)
    {
        dbContext.EnsureDatabase();
        Product product = dbContext.Products.Find(id);
        if (product != null)
        {
            dbContext.Products.Remove(product);
            dbContext.SaveChanges();
        }
    }
}

public class MarkingRecordService(AppDbContext dbContext)
{
    public void AddMarkingRecord(MarkingRecord record)
    {
        dbContext.EnsureDatabase();
        dbContext.MarkingRecords.Add(record);
        dbContext.SaveChanges();
    }

    public MarkingRecord GetMarkingRecord(int id)
    {
        dbContext.EnsureDatabase();
        return dbContext.MarkingRecords.Find(id);
    }

    public List<MarkingRecord> GetMarkingRecords()
    {
        dbContext.EnsureDatabase();
        return dbContext.MarkingRecords
            .Include(record => record.Product)
            .OrderByDescending(record => record.CreatedAt)
            .ToList();
    }

    public void UpdateMarkingRecord(MarkingRecord record)
    {
        dbContext.EnsureDatabase();
        dbContext.Entry(record).State = EntityState.Modified;
        dbContext.SaveChanges();
    }

    public void DeleteMarkingRecord(int id)
    {
        dbContext.EnsureDatabase();
        MarkingRecord record = dbContext.MarkingRecords.Find(id);
        if (record != null)
        {
            dbContext.MarkingRecords.Remove(record);
            dbContext.SaveChanges();
        }
    }
}
