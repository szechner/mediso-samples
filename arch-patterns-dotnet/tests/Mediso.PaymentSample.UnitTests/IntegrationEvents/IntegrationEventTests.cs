using Mediso.PaymentSample.Domain;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.IntegrationEvents;

[TestFixture]
public class IntegrationEventTests
{
    [Test]
    public void IntegrationEvent_ShouldImplementIIntegrationEvent()
    {
        // Arrange
        var paymentId = PaymentId.New();
        var payerAccountId = AccountId.New("ACC-001");
        var payeeAccountId = AccountId.New("ACC-002");
        var amount = new Money(100m, new Currency("USD"));
        var reference = "Test payment";

        // Act
        var integrationEvent = new PaymentCreatedIntegrationEvent(
            paymentId, payerAccountId, payeeAccountId, amount, reference);

        // Assert
        Assert.That(integrationEvent, Is.AssignableTo<IIntegrationEvent>());
        Assert.That(integrationEvent.EventId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(integrationEvent.CreatedAt, Is.LessThanOrEqualTo(DateTimeOffset.UtcNow));
        Assert.That(integrationEvent.EventType, Is.EqualTo(nameof(PaymentCreatedIntegrationEvent)));
        Assert.That(integrationEvent.Version, Is.EqualTo(2)); // PaymentCreated overrides to version 2
        Assert.That(integrationEvent.CorrelationId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void IntegrationEvent_ShouldHaveUniqueEventId()
    {
        // Arrange & Act
        var event1 = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0");
        var event2 = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), false, "v1.0");

        // Assert
        Assert.That(event1.EventId, Is.Not.EqualTo(event2.EventId));
    }

    [Test]
    public void IntegrationEvent_ShouldHaveUniqueCorrelationIdByDefault()
    {
        // Arrange & Act
        var event1 = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0");
        var event2 = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), false, "v1.0");

        // Assert
        Assert.That(event1.CorrelationId, Is.Not.EqualTo(event2.CorrelationId));
    }

    [Test]
    public void IntegrationEvent_ShouldAllowCustomCorrelationId()
    {
        // Arrange
        var customCorrelationId = "CUSTOM-CORRELATION-123";
        var paymentId = PaymentId.New();

        // Act
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            paymentId, true, "v1.0")
        {
            CorrelationId = customCorrelationId
        };

        // Assert
        Assert.That(integrationEvent.CorrelationId, Is.EqualTo(customCorrelationId));
    }

    [Test]
    public void IntegrationEvent_ShouldAllowCustomEventId()
    {
        // Arrange
        var customEventId = Guid.NewGuid();
        var paymentId = PaymentId.New();

        // Act
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            paymentId, true, "v1.0")
        {
            EventId = customEventId
        };

        // Assert
        Assert.That(integrationEvent.EventId, Is.EqualTo(customEventId));
    }

    [Test]
    public void IntegrationEvent_ShouldAllowCustomCreatedAt()
    {
        // Arrange
        var customCreatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var paymentId = PaymentId.New();

        // Act
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            paymentId, true, "v1.0")
        {
            CreatedAt = customCreatedAt
        };

        // Assert
        Assert.That(integrationEvent.CreatedAt, Is.EqualTo(customCreatedAt));
    }

    [Test]
    public void PaymentCreatedIntegrationEvent_ShouldHaveCorrectEventType()
    {
        // Arrange & Act
        var integrationEvent = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment");

        // Assert
        Assert.That(integrationEvent.EventType, Is.EqualTo("PaymentCreatedIntegrationEvent"));
    }

    [Test]
    public void PaymentCreatedIntegrationEvent_ShouldHaveVersion2()
    {
        // Arrange & Act
        var integrationEvent = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment");

        // Assert
        Assert.That(integrationEvent.Version, Is.EqualTo(2));
    }

    [Test]
    public void AMLScreeningCompletedIntegrationEvent_ShouldHaveVersion1()
    {
        // Arrange & Act
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0");

        // Assert
        Assert.That(integrationEvent.Version, Is.EqualTo(1));
    }

    [Test]
    public void FundsReservationRequestedIntegrationEvent_ShouldHaveDefaultVersion()
    {
        // Arrange & Act
        var integrationEvent = new FundsReservationRequestedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            new Money(100m, new Currency("USD")));

        // Assert
        Assert.That(integrationEvent.Version, Is.EqualTo(1)); // Default version
    }

    [Test]
    public void IntegrationEvent_CreatedAt_ShouldBeRecentForNewEvents()
    {
        // Arrange
        var beforeCreation = DateTimeOffset.UtcNow;
        
        // Act
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0");
        
        var afterCreation = DateTimeOffset.UtcNow;

        // Assert
        Assert.That(integrationEvent.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreation));
        Assert.That(integrationEvent.CreatedAt, Is.LessThanOrEqualTo(afterCreation));
    }

    [Test]
    public void IntegrationEvent_ShouldPreserveAllPropertiesInWith()
    {
        // Arrange
        var originalEvent = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0");
        var newCorrelationId = "NEW-CORRELATION-123";

        // Act
        var modifiedEvent = originalEvent with { CorrelationId = newCorrelationId };

        // Assert
        Assert.That(modifiedEvent.PaymentId, Is.EqualTo(originalEvent.PaymentId));
        Assert.That(modifiedEvent.Passed, Is.EqualTo(originalEvent.Passed));
        Assert.That(modifiedEvent.RuleSetVersion, Is.EqualTo(originalEvent.RuleSetVersion));
        Assert.That(modifiedEvent.EventId, Is.EqualTo(originalEvent.EventId));
        Assert.That(modifiedEvent.CreatedAt, Is.EqualTo(originalEvent.CreatedAt));
        Assert.That(modifiedEvent.EventType, Is.EqualTo(originalEvent.EventType));
        Assert.That(modifiedEvent.Version, Is.EqualTo(originalEvent.Version));
        Assert.That(modifiedEvent.CorrelationId, Is.EqualTo(newCorrelationId));
    }
}