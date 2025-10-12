using FluentValidation;
using Mediso.PaymentSample.Application.Modules.Payments.Contracts;

namespace Mediso.PaymentSample.Application.Modules.Payments.Validators;

/// <summary>
/// FluentValidation validator for InitiatePaymentCommand.
/// Validates business rules and data constraints for payment initiation.
/// </summary>
public sealed class InitiatePaymentCommandValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required");

        RuleFor(x => x.MerchantId)
            .NotEmpty()
            .WithMessage("MerchantId is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-character ISO code")
            .Matches("^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters only");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .WithMessage("PaymentMethod is required")
            .MaximumLength(50)
            .WithMessage("PaymentMethod cannot exceed 50 characters");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required")
            .MaximumLength(100)
            .WithMessage("CorrelationId cannot exceed 100 characters");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey is required")
            .MaximumLength(100)
            .WithMessage("IdempotencyKey cannot exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.ProcessingTimeout)
            .GreaterThan(TimeSpan.Zero)
            .When(x => x.ProcessingTimeout.HasValue)
            .WithMessage("ProcessingTimeout must be positive")
            .LessThanOrEqualTo(TimeSpan.FromMinutes(30))
            .When(x => x.ProcessingTimeout.HasValue)
            .WithMessage("ProcessingTimeout cannot exceed 30 minutes");

        RuleFor(x => x.Metadata)
            .Must(HaveValidMetadataSize)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata cannot exceed 10 entries or 1000 characters per value");
    }

    private static bool HaveValidMetadataSize(Dictionary<string, string>? metadata)
    {
        if (metadata == null) return true;
        
        return metadata.Count <= 10 && 
               metadata.Values.All(v => v?.Length <= 1000);
    }
}