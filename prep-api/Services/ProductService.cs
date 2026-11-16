using PrepApi.Models;
namespace PrepApi.Services;

public interface IProductService
{
    IEnumerable<Product> GetAllProducts();
}

public class ProductService : IProductService
{
    private readonly List<Product> _products;

    public ProductService()
    {
        _products = new List<Product>
        {
            new Product(1, "Product 1", "Description 1", 10.99m),
            new Product(2, "Product 2", "Description 2", 20.99m),
            new Product(3, "Product 3", "Description 3", 30.99m)
        };
    }

    public IEnumerable<Product> GetAllProducts()
    {
        return _products;
    }
}