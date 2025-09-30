using Mediso.PaymentSample.Domain;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.Examples;

/// <summary>
/// Examples demonstrating the usage of Version and CorrelationId in integration events
/// </summary>
public static class IntegrationEventExamples
{
    /// <summary>
    /// Example: Creating correlated events in a payment processing workflow
    /// </summary>
    public static void PaymentWorkflowCorrelationExample()
    {
        // 1. Start a new payment workflow with a correlation ID
        var correlationId = IntegrationEventUtilities.NewCorrelationId();
        
        // 2. Create the initial payment event with the correlation ID
        var paymentCreated = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"), 
            new Money(100m, new Currency("USD")),
            "Payment for invoice #123"
        ) { CorrelationId = correlationId };

        Console.WriteLine($"Payment created with correlation ID: {paymentCreated.CorrelationId}");
        Console.WriteLine($"Event version: {paymentCreated.Version}");

        // 3. Create a related AML screening event with the same correlation ID
        var amlScreeningCompleted = IntegrationEventUtilities.CorrelateWith(
            () => new AMLScreeningCompletedIntegrationEvent(
                paymentCreated.PaymentId,
                passed: true,
                ruleSetVersion: "v2.1"
            ),
            paymentCreated
        );

        Console.WriteLine($"AML screening correlated: {IntegrationEventUtilities.AreCorrelated(paymentCreated, amlScreeningCompleted)}");

        // 4. Create funds reservation request with correlation
        var fundsReservationRequested = new FundsReservationRequestedIntegrationEvent(
            paymentCreated.PaymentId,
            paymentCreated.PayerAccountId,
            paymentCreated.Amount
        ) { CorrelationId = correlationId };

        // 5. Demonstrate correlation context usage
        using (IntegrationEventCorrelationContext.CreateScope(correlationId))
        {
            // Any integration events created in this scope will automatically
            // have access to the correlation ID through the ambient context
            var currentCorrelationId = IntegrationEventCorrelationContext.Current;
            Console.WriteLine($"Current correlation ID in scope: {currentCorrelationId}");
        }
    }

    /// <summary>
    /// Example: Working with event versions and compatibility
    /// </summary>
    public static void EventVersioningExample()
    {
        // Register version information for payment events
        IntegrationEventVersionRegistry.RegisterEventVersion(
            nameof(PaymentCreatedIntegrationEvent),
            currentVersion: 2,
            supportedVersions: new[] { 1, 2 }
        );

        IntegrationEventVersionRegistry.RegisterEventVersion(
            nameof(AMLScreeningCompletedIntegrationEvent),
            currentVersion: 1,
            supportedVersions: new[] { 1 }
        );

        // Check version compatibility
        var paymentEventType = nameof(PaymentCreatedIntegrationEvent);
        var isV1Supported = IntegrationEventVersionRegistry.IsVersionSupported(paymentEventType, 1);
        var isV2Supported = IntegrationEventVersionRegistry.IsVersionSupported(paymentEventType, 2);
        var isV3Supported = IntegrationEventVersionRegistry.IsVersionSupported(paymentEventType, 3);

        Console.WriteLine($"PaymentCreated v1 supported: {isV1Supported}"); // True
        Console.WriteLine($"PaymentCreated v2 supported: {isV2Supported}"); // True  
        Console.WriteLine($"PaymentCreated v3 supported: {isV3Supported}"); // False

        var currentVersion = IntegrationEventVersionRegistry.GetCurrentVersion(paymentEventType);
        Console.WriteLine($"Current version: {currentVersion}"); // 2
    }

    /// <summary>
    /// Example: Creating events with explicit correlation and version control
    /// </summary>
    public static void ExplicitCorrelationAndVersionExample()
    {
        var customCorrelationId = "PAYMENT-WORKFLOW-2024-001";
        
        // Create a payment event with explicit correlation ID
        var paymentEvent = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("BUSINESS-001"),
            AccountId.New("VENDOR-002"),
            new Money(1500.00m, new Currency("EUR")),
            "Invoice payment - Q4 services"
        ) 
        { 
            CorrelationId = customCorrelationId 
        };

        // Create related events in the same correlation chain
        var events = new List<IntegrationEvent>
        {
            paymentEvent,
            
            new AMLScreeningCompletedIntegrationEvent(
                paymentEvent.PaymentId,
                passed: true,
                ruleSetVersion: "v3.0",
                severity: "LOW"
            ) { CorrelationId = customCorrelationId },
            
            new FundsReservationCompletedIntegrationEvent(
                paymentEvent.PaymentId,
                paymentEvent.PayerAccountId,
                success: true,
                reservationId: ReservationId.New()
            ) { CorrelationId = customCorrelationId }
        };

        // Verify all events are correlated
        var allCorrelated = events.Skip(1).All(e => 
            IntegrationEventUtilities.AreCorrelated(paymentEvent, e));
        
        Console.WriteLine($"All events in workflow are correlated: {allCorrelated}");
        
        // Display event information
        foreach (var evt in events)
        {
            Console.WriteLine($"Event: {evt.EventType}, Version: {evt.Version}, " +
                            $"Correlation: {evt.CorrelationId[..8]}..., " +
                            $"Created: {evt.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
    }

    /// <summary>
    /// Example: Handling correlation across module boundaries
    /// </summary>
    public static async Task CrossModuleCommunicationExample()
    {
        var workflowCorrelationId = IntegrationEventUtilities.NewCorrelationId();
        
        // Simulate payment module publishing an event
        var paymentCreated = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("CUSTOMER-123"),
            AccountId.New("MERCHANT-456"),
            new Money(250.00m, new Currency("GBP")),
            "Online purchase"
        ) { CorrelationId = workflowCorrelationId };

        // Simulate compliance module receiving and responding
        using (IntegrationEventCorrelationContext.CreateScope(paymentCreated))
        {
            // Inside this scope, any new events will automatically
            // inherit the correlation context
            await SimulateComplianceModuleProcessing(paymentCreated);
        }

        // Simulate accounts module processing
        using (IntegrationEventCorrelationContext.CreateScope(paymentCreated))
        {
            await SimulateAccountsModuleProcessing(paymentCreated);
        }
    }

    private static async Task SimulateComplianceModuleProcessing(PaymentCreatedIntegrationEvent paymentEvent)
    {
        // Simulate async compliance processing
        await Task.Delay(100);
        
        // Create compliance response with current correlation context
        var amlResult = new AMLScreeningCompletedIntegrationEvent(
            paymentEvent.PaymentId,
            passed: true,
            ruleSetVersion: "v2.5"
        ) 
        { 
            CorrelationId = IntegrationEventCorrelationContext.Current ?? 
                           IntegrationEventUtilities.NewCorrelationId()
        };

        Console.WriteLine($"Compliance module processed payment with correlation: {amlResult.CorrelationId[..8]}...");
    }

    private static async Task SimulateAccountsModuleProcessing(PaymentCreatedIntegrationEvent paymentEvent)
    {
        // Simulate async account processing
        await Task.Delay(150);
        
        var reservationResult = new FundsReservationCompletedIntegrationEvent(
            paymentEvent.PaymentId,
            paymentEvent.PayerAccountId,
            success: true,
            reservationId: ReservationId.New()
        )
        {
            CorrelationId = IntegrationEventCorrelationContext.Current ??
                           IntegrationEventUtilities.NewCorrelationId()
        };

        Console.WriteLine($"Accounts module processed payment with correlation: {reservationResult.CorrelationId[..8]}...");
    }
}