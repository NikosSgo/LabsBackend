using AutoMapper;
using Messages;
using Models.Dto.V1.Requests;
using Oms.BLL.Models;
using Oms.DAL.Models;
using AuditLogOrderDto = Models.Dto.Common.AuditLogOrder;
using OrderItemUnitDto = Models.Dto.Common.OrderItemUnit;
using OrderUnitDto = Models.Dto.Common.OrderUnit;

namespace Oms.Config;

/// <summary>
/// Centralized AutoMapper profile for controller-level mappings.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<V1CreateOrderRequest.OrderItem, OrderItemUnit>();
        CreateMap<V1CreateOrderRequest.Order, OrderUnit>();
        CreateMap<V1QueryOrdersRequest, QueryOrdersModel>();
        CreateMap<V1UpdateOrdersStatusRequest, UpdateOrdersStatusModel>();

        CreateMap<OrderUnit, OrderUnitDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<OrderUnit, V1OrderDal>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

        CreateMap<OrderItemUnit, OrderItemUnitDto>();
        CreateMap<OrderItemUnit, V1OrderItemDal>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.OrderId, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
        CreateMap<AuditLogOrder, AuditLogOrderDto>();
        CreateMap<AuditLogOrder, V1AuditLogOrderDal>();
        CreateMap<AuditLogOrderDto, AuditLogOrder>();

        CreateMap<V1OrderDal, OrderUnit>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                Enum.Parse<OrderUnit.OrderStatus>(src.Status, true)));
        CreateMap<OrderUnit, V1OrderDal>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<V1OrderItemDal, OrderItemUnit>();
        CreateMap<V1AuditLogOrderDal, AuditLogOrder>();

        // Маппинги для RabbitMQ сообщений
        CreateMap<V1OrderDal, OmsOrderCreatedMessage>();
        CreateMap<V1OrderItemDal, OmsOrderCreatedMessage.OrderItemUnit>();
    }
}
