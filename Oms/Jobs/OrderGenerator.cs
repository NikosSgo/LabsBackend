using AutoFixture;
using WebApi.BLL.Models;
using WebApi.BLL.Services;

namespace WebApi.Jobs;

public class OrderGenerator(IServiceProvider serviceProvider, ILogger<OrderGenerator> logger): BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fixture = new Fixture();
        using var scope = serviceProvider.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var orders = Enumerable.Range(1, 50)
                    .Select(_ =>
                    {
                        var orderItem = fixture.Build<OrderItemUnit>()
                            .With(x => x.PriceCurrency, "RUB")
                            .With(x => x.PriceCents, 1000)
                            .Create();

                        var order = fixture.Build<OrderUnit>()
                            .With(x => x.TotalPriceCurrency, "RUB")
                            .With(x => x.TotalPriceCents, 1000)
                            .With(x => x.OrderItems, [orderItem])
                            .Create();

                        return order;
                    })
                    .ToArray();

                await orderService.BatchInsert(orders, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "OrderGenerator failed, will retry");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

                await Task.Delay(250, stoppingToken);
        }
    }
}
