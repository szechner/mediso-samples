using Mediso.PaymentSample.Domain;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.IntegrationEvents;

[TestFixture]
public class IntegrationEventUtilitiesTests
{
    [Test]
    public void NewCorrelationId_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = IntegrationEventUtilities.NewCorrelationId();
        var id2 = IntegrationEventUtilities.NewCorrelationId();

        // Assert
        Assert.That(id1, Is.Not.Null.And.Not.Empty);
        Assert.That(id2, Is.Not.Null.And.Not.Empty);
        Assert.That(id1, Is.Not.EqualTo(id2));
        
        // Should be valid GUID format
        Assert.That(Guid.TryParse(id1, out _), Is.True);
        Assert.That(Guid.TryParse(id2, out _), Is.True);
    }

    [Test]
    public void WithCorrelationId_ShouldSetSpecificCorrelationId()
    {
        // Arrange
        var correlationId = "SPECIFIC-CORRELATION-123";
        
        // Act
        var integrationEvent = IntegrationEventUtilities.WithCorrelationId(
            () => new AMLScreeningCompletedIntegrationEvent(PaymentId.New(), true, "v1.0"),
            correlationId);

        // Assert
        Assert.That(integrationEvent.CorrelationId, Is.EqualTo(correlationId));
    }

    [Test]
    public void WithCorrelationId_ShouldPreserveOtherProperties()
    {
        // Arrange
        var paymentId = PaymentId.New();
        var correlationId = "SPECIFIC-CORRELATION-123";
        
        // Act
        var integrationEvent = IntegrationEventUtilities.WithCorrelationId(
            () => new AMLScreeningCompletedIntegrationEvent(paymentId, true, "v1.0"),
            correlationId);

        // Assert
        Assert.That(integrationEvent.PaymentId, Is.EqualTo(paymentId));
        Assert.That(integrationEvent.Passed, Is.True);
        Assert.That(integrationEvent.RuleSetVersion, Is.EqualTo("v1.0"));
        Assert.That(integrationEvent.CorrelationId, Is.EqualTo(correlationId));
    }

    [Test]
    public void CorrelateWith_ShouldUseSameCorrelationId()
    {
        // Arrange
        var originalEvent = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment");

        // Act
        var correlatedEvent = IntegrationEventUtilities.CorrelateWith(
            () => new AMLScreeningCompletedIntegrationEvent(originalEvent.PaymentId, true, "v1.0"),
            originalEvent);

        // Assert
        Assert.That(correlatedEvent.CorrelationId, Is.EqualTo(originalEvent.CorrelationId));
        Assert.That(correlatedEvent.PaymentId, Is.EqualTo(originalEvent.PaymentId));
    }

    [Test]
    public void AreCorrelated_ShouldReturnTrueForSameCorrelationId()
    {
        // Arrange
        var correlationId = "SHARED-CORRELATION-123";
        var event1 = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment")
        {
            CorrelationId = correlationId
        };
        
        var event2 = new AMLScreeningCompletedIntegrationEvent(
            event1.PaymentId, true, "v1.0")
        {
            CorrelationId = correlationId
        };

        // Act
        var areCorrelated = IntegrationEventUtilities.AreCorrelated(event1, event2);

        // Assert
        Assert.That(areCorrelated, Is.True);
    }

    [Test]
    public void AreCorrelated_ShouldReturnFalseForDifferentCorrelationId()
    {
        // Arrange
        var event1 = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment")
        {
            CorrelationId = "CORRELATION-123"
        };
        
        var event2 = new AMLScreeningCompletedIntegrationEvent(
            event1.PaymentId, true, "v1.0")
        {
            CorrelationId = "CORRELATION-456"
        };

        // Act
        var areCorrelated = IntegrationEventUtilities.AreCorrelated(event1, event2);

        // Assert
        Assert.That(areCorrelated, Is.False);
    }

    [Test]
    public void AreCorrelated_ShouldBeCaseInsensitive()
    {
        // Arrange
        var event1 = new PaymentCreatedIntegrationEvent(
            PaymentId.New(),
            AccountId.New("ACC-001"),
            AccountId.New("ACC-002"),
            new Money(100m, new Currency("USD")),
            "Test payment")
        {
            CorrelationId = "CORRELATION-123"
        };
        
        var event2 = new AMLScreeningCompletedIntegrationEvent(
            event1.PaymentId, true, "v1.0")
        {
            CorrelationId = "correlation-123"
        };

        // Act
        var areCorrelated = IntegrationEventUtilities.AreCorrelated(event1, event2);

        // Assert
        Assert.That(areCorrelated, Is.True);
    }

    [Test]
    public void GetOrGenerateCorrelationId_ShouldReturnEventCorrelationId_WhenEventHasOne()
    {
        // Arrange
        var expectedCorrelationId = "EXISTING-CORRELATION-123";
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0")
        {
            CorrelationId = expectedCorrelationId
        };

        // Act
        var correlationId = IntegrationEventUtilities.GetOrGenerateCorrelationId(integrationEvent);

        // Assert
        Assert.That(correlationId, Is.EqualTo(expectedCorrelationId));
    }

    [Test]
    public void GetOrGenerateCorrelationId_ShouldGenerateNewId_WhenEventIsNull()
    {
        // Act
        var correlationId = IntegrationEventUtilities.GetOrGenerateCorrelationId(null);

        // Assert
        Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
        Assert.That(Guid.TryParse(correlationId, out _), Is.True);
    }

    [Test]
    public void GetOrGenerateCorrelationId_ShouldGenerateUniqueIds_WhenCalledMultipleTimes()
    {
        // Act
        var id1 = IntegrationEventUtilities.GetOrGenerateCorrelationId(null);
        var id2 = IntegrationEventUtilities.GetOrGenerateCorrelationId(null);

        // Assert
        Assert.That(id1, Is.Not.EqualTo(id2));
    }
}