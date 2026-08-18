using Microsoft.EntityFrameworkCore;

namespace HansLaserDateSerialDemo;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<MarkingRecord> MarkingRecords { get; set; }

    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.db");

    protected override void OnConfiguring(DbContextOptionsBuilder options) =>
        options.UseSqlite($"Data Source={DbPath}");
}

public class ProductService(AppDbContext dbContext)
{
    public void AddProduct(Product product)
    {
        dbContext.Database.EnsureCreated();
        dbContext.Products.Add(product);
        dbContext.SaveChanges();
    }

    public Product GetProduct(int id)
    {
        dbContext.Database.EnsureCreated();
        return dbContext.Products.Find(id);
    }

    public List<Product> GetProducts()
    {
        dbContext.Database.EnsureCreated();
        return dbContext.Products.OrderBy(product => product.Name).ThenBy(product => product.CustomerPartNumber).ToList();
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
        dbContext.Database.EnsureCreated();
        dbContext.Entry(product).State = EntityState.Modified;
        dbContext.SaveChanges();
    }

    public void DeleteProduct(int id)
    {
        dbContext.Database.EnsureCreated();
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
        dbContext.MarkingRecords.Add(record);
        dbContext.SaveChanges();
    }

    public MarkingRecord GetMarkingRecord(int id)
    {
        return dbContext.MarkingRecords.Find(id);
    }

    public List<MarkingRecord> GetMarkingRecords()
    {
        return dbContext.MarkingRecords.ToList();
    }

    public void UpdateMarkingRecord(MarkingRecord record)
    {
        dbContext.Entry(record).State = EntityState.Modified;
        dbContext.SaveChanges();
    }

    public void DeleteMarkingRecord(int id)
    {
        MarkingRecord record = dbContext.MarkingRecords.Find(id);
        if (record != null)
        {
            dbContext.MarkingRecords.Remove(record);
            dbContext.SaveChanges();
        }
    }
}
