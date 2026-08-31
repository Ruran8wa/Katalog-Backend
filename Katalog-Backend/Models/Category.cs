namespace Katalog_Backend.Models;

public class Category
{
    public  int Id { get; set; }
    public string CategoryName {get; set;} =  string.Empty;
    public int? CategoryParentId { get; set; }
    
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; }
    public ICollection<Product> Products { get; set; }
}