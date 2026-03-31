namespace FlashSales.Api.Infrastructure;

public class OutOfStockException : Exception
{
    public OutOfStockException()
        : base("out of stock or campaign not active") { }
}
