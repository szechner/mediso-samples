using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.Domain.Specifications;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.Domain;

[TestFixture]
public class PaymentSpecsTests
{
    [Test]
    public void CanBeCancelled_WithRequestedState_ShouldReturnTrue()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Requested);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanBeCancelled_WithFlaggedState_ShouldReturnTrue()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Flagged);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanBeCancelled_WithReleasedState_ShouldReturnTrue()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Released);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void CanBeCancelled_WithDeclinedState_ShouldReturnFalse()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Declined);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanBeCancelled_WithReservedState_ShouldReturnFalse()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Reserved);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanBeCancelled_WithJournaledState_ShouldReturnFalse()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Journaled);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanBeCancelled_WithSettledState_ShouldReturnFalse()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Settled);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void CanBeCancelled_WithFailedState_ShouldReturnFalse()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Failed);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.False);
    }

    [TestCase(PaymentState.Requested, true)]
    [TestCase(PaymentState.Flagged, true)]
    [TestCase(PaymentState.Released, true)]
    [TestCase(PaymentState.Declined, false)]
    [TestCase(PaymentState.Reserved, false)]
    [TestCase(PaymentState.Journaled, false)]
    [TestCase(PaymentState.Settled, false)]
    [TestCase(PaymentState.Failed, false)]
    public void CanBeCancelled_WithVariousStates_ShouldReturnExpectedResult(PaymentState state, bool expectedResult)
    {
        // Arrange
        var payment = CreatePaymentInState(state);

        // Act
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public void CanBeCancelled_WithNullPayment_ShouldThrowNullReferenceException()
    {
        // Arrange
        Payment? payment = null;

        // Act & Assert
        Assert.Throws<NullReferenceException>(() => payment!.CanBeCancelled());
    }

    [Test]
    public void CanBeCancelled_ShouldBeExtensionMethod()
    {
        // This test verifies that CanBeCancelled is available as an extension method
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Requested);

        // Act - Should be callable as extension method without explicit static class reference
        var result = payment.CanBeCancelled();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void PaymentSpecs_ShouldBeStaticClass()
    {
        // Arrange & Act
        var type = typeof(PaymentSpecs);

        // Assert
        Assert.That(type.IsAbstract, Is.True);
        Assert.That(type.IsSealed, Is.True);
    }

    [Test]
    public void CanBeCancelled_WithMultipleCalls_ShouldReturnConsistentResult()
    {
        // Arrange
        var payment = CreatePaymentInState(PaymentState.Requested);

        // Act
        var result1 = payment.CanBeCancelled();
        var result2 = payment.CanBeCancelled();
        var result3 = payment.CanBeCancelled();

        // Assert
        Assert.That(result1, Is.True);
        Assert.That(result2, Is.True);
        Assert.That(result3, Is.True);
        Assert.That(result1, Is.EqualTo(result2));
        Assert.That(result2, Is.EqualTo(result3));
    }

    /// <summary>
    /// Helper method to create a payment in a specific state for testing
    /// </summary>
    private static Payment CreatePaymentInState(PaymentState state)
    {
        var paymentId = PaymentId.New();
        var amount = new Money(100m, new Currency("USD"));
        var payerAccountId = new AccountId("PAYER123");
        var payeeAccountId = new AccountId("PAYEE456");
        var reference = "Test payment";
        
        var payment = Payment.Create(paymentId, amount, payerAccountId, payeeAccountId, reference);
        
        // Transition to the required state
        switch (state)
        {
            case PaymentState.Requested:
                // Already in this state
                break;
            case PaymentState.Flagged:
                payment.Flag("Test reason", "Low");
                break;
            case PaymentState.Released:
                payment.Flag("Test reason", "Low");
                payment.ReleaseAfterFlag();
                break;
            case PaymentState.Reserved:
                payment.ReserveFunds(ReservationId.New());
                break;
            case PaymentState.Journaled:
                payment.ReserveFunds(ReservationId.New());
                var entries = new List<LedgerEntry>
                {
                    new(LedgerEntryId.New(), payerAccountId, payeeAccountId, amount)
                };
                payment.Journal(entries);
                break;
            case PaymentState.Settled:
                payment.ReserveFunds(ReservationId.New());
                var entries2 = new List<LedgerEntry>
                {
                    new(LedgerEntryId.New(), payerAccountId, payeeAccountId, amount)
                };
                payment.Journal(entries2);
                payment.Settle("SWIFT", "EXT123");
                break;
            case PaymentState.Declined:
                payment.Cancel("user");
                break;
            case PaymentState.Failed:
                payment.Fail("Test failure");
                break;
        }
        
        return payment;
    }
}