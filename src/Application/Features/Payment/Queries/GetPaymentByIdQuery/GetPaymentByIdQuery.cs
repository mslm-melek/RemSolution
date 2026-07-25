using RemSolution.Application.Common.Interfaces;
using RemSolution.Application.Common.Security;
using RemSolution.Application.Features.Payment.DTOs;
using RemSolution.Domain.Constants;

namespace RemSolution.Application.Features.Payment.Queries.GetPaymentByIdQuery
{
    [Authorize(Policy = Permissions.PaymentRead)]
    [RequiresFeature(FeatureFlags.Payments)]
    public record GetPaymentByIdQuery(int Id) : IRequest<PaymentDto?>;

    public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto?>
    {
        private readonly IApplicationDbContext _context;

        public GetPaymentByIdQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentDto?> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
        {
            var payment = await _context.Payments
                .Where(p => p.Id == request.Id)
                .ProjectToType<PaymentDto>()
                .FirstOrDefaultAsync(cancellationToken);

            if (payment == null)
                throw new NotFoundException("Payment", request.Id.ToString());

            return payment;
        }
    }
}
