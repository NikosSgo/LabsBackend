using AutoFixture;
using Oms.BLL.Models;
using Oms.BLL.Services;

namespace Oms.Jobs;

public class OrderGenerator(IServiceProvider serviceProvider, ILogger<OrderGenerator> logger): BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fixture = new Fixture();
        using var scope = serviceProvider.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        var random = new Random();

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

                var created = await orderService.BatchInsert(orders, stoppingToken);

                if (created.Length > 0)
                {
                    var statusValues = Enum.GetValues<OrderUnit.OrderStatus>();

                    // берём случайное количество заказов из текущей пачки
                    var toUpdateCount = random.Next(1, created.Length + 1);
                    var toUpdate = created.OrderBy(_ => random.Next()).Take(toUpdateCount).ToArray();

                    var updates = toUpdate.Select(order => new UpdateOrdersStatusModel
                    {
                        OrderIds = new[] { order.Id },
                        NewStatus = statusValues[random.Next(statusValues.Length)].ToString()
                    }).ToArray();

                    await orderService.BatchUpdateStatus(updates, stoppingToken);
                }
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
