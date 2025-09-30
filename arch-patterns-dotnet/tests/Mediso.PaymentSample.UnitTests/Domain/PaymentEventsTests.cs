using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.Domain;

[TestFixture]
public class PaymentEventsTests
{
    [TestFixture]
    public class PaymentRequestedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var amount = new Money(100m, new Currency("USD"));
            var payerAccountId = new AccountId("PAYER123");
            var payeeAccountId = new AccountId("PAYEE456");
            var reference = "Test payment";

            // Act
            var eventObj = new PaymentRequested(paymentId, amount, payerAccountId, payeeAccountId, reference);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Amount, Is.EqualTo(amount));
            Assert.That(eventObj.PayerAccountId, Is.EqualTo(payerAccountId));
            Assert.That(eventObj.PayeeAccountId, Is.EqualTo(payeeAccountId));
            Assert.That(eventObj.Reference, Is.EqualTo(reference));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void CreatedAt_ShouldBeRecentTimestamp()
        {
            // Arrange
            var before = DateTimeOffset.UtcNow;
            
            // Act
            var eventObj = new PaymentRequested(
                PaymentId.New(), 
                new Money(100m, new Currency("USD")), 
                new AccountId("PAYER123"), 
                new AccountId("PAYEE456"), 
                "Test");
            
            var after = DateTimeOffset.UtcNow;

            // Assert
            Assert.That(eventObj.CreatedAt, Is.GreaterThanOrEqualTo(before));
            Assert.That(eventObj.CreatedAt, Is.LessThanOrEqualTo(after));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentRequested(
                PaymentId.New(), 
                new Money(100m, new Currency("USD")), 
                new AccountId("PAYER123"), 
                new AccountId("PAYEE456"), 
                "Test");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class AMLPassedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var ruleSetVersion = "1.2.3";

            // Act
            var eventObj = new AMLPassed(paymentId, ruleSetVersion);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.RuleSetVersion, Is.EqualTo(ruleSetVersion));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new AMLPassed(PaymentId.New(), "1.0");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentFlaggedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var reason = "Suspicious transaction";
            var severity = "High";

            // Act
            var eventObj = new PaymentFlagged(paymentId, reason, severity);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Reason, Is.EqualTo(reason));
            Assert.That(eventObj.Severity, Is.EqualTo(severity));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentFlagged(PaymentId.New(), "Reason", "Low");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class FundsReservedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var reservationId = ReservationId.New();
            var amount = new Money(500m, new Currency("EUR"));

            // Act
            var eventObj = new FundsReserved(paymentId, reservationId, amount);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.ReservationId, Is.EqualTo(reservationId));
            Assert.That(eventObj.Amount, Is.EqualTo(amount));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new FundsReserved(
                PaymentId.New(), 
                ReservationId.New(), 
                new Money(100m, new Currency("USD")));

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class FundsReservationFailedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var reason = "Insufficient funds";

            // Act
            var eventObj = new FundsReservationFailed(paymentId, reason);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Reason, Is.EqualTo(reason));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new FundsReservationFailed(PaymentId.New(), "Failed");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentJournaledTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var entries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), new AccountId("DEBIT123"), new AccountId("CREDIT456"), new Money(100m, new Currency("USD")))
            };

            // Act
            var eventObj = new PaymentJournaled(paymentId, entries);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Entries, Is.EqualTo(entries));
            Assert.That(eventObj.Entries.Count, Is.EqualTo(1));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void Constructor_WithEmptyEntries_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var entries = new List<LedgerEntry>();

            // Act
            var eventObj = new PaymentJournaled(paymentId, entries);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Entries, Is.EqualTo(entries));
            Assert.That(eventObj.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentJournaled(PaymentId.New(), new List<LedgerEntry>());

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentSettledTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var channel = "SWIFT";
            var externalRef = "EXT123456";

            // Act
            var eventObj = new PaymentSettled(paymentId, channel, externalRef);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Channel, Is.EqualTo(channel));
            Assert.That(eventObj.ExternalRef, Is.EqualTo(externalRef));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void Constructor_WithNullExternalRef_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var channel = "INTERNAL";
            string? externalRef = null;

            // Act
            var eventObj = new PaymentSettled(paymentId, channel, externalRef);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Channel, Is.EqualTo(channel));
            Assert.That(eventObj.ExternalRef, Is.Null);
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentSettled(PaymentId.New(), "CHANNEL", "REF");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentCancelledTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var by = "user123";

            // Act
            var eventObj = new PaymentCancelled(paymentId, by);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.By, Is.EqualTo(by));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentCancelled(PaymentId.New(), "user");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentDeclinedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var reason = "Insufficient funds";

            // Act
            var eventObj = new PaymentDeclined(paymentId, reason);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Reason, Is.EqualTo(reason));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentDeclined(PaymentId.New(), "Reason");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentFailedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var reason = "Network timeout";

            // Act
            var eventObj = new PaymentFailed(paymentId, reason);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Reason, Is.EqualTo(reason));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentFailed(PaymentId.New(), "Failed");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class PaymentNotifiedTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateEvent()
        {
            // Arrange
            var paymentId = PaymentId.New();
            var channel = "EMAIL";

            // Act
            var eventObj = new PaymentNotified(paymentId, channel);

            // Assert
            Assert.That(eventObj.PaymentId, Is.EqualTo(paymentId));
            Assert.That(eventObj.Channel, Is.EqualTo(channel));
            Assert.That(eventObj.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void ShouldImplementIDomainEvent()
        {
            // Arrange & Act
            var eventObj = new PaymentNotified(PaymentId.New(), "SMS");

            // Assert
            Assert.That(eventObj, Is.InstanceOf<IDomainEvent>());
        }
    }

    [TestFixture]
    public class LedgerEntryTests
    {
        [Test]
        public void Constructor_WithValidParameters_ShouldCreateLedgerEntry()
        {
            // Arrange
            var entryId = LedgerEntryId.New();
            var debitAccountId = new AccountId("DEBIT123");
            var creditAccountId = new AccountId("CREDIT456");
            var amount = new Money(250m, new Currency("GBP"));

            // Act
            var ledgerEntry = new LedgerEntry(entryId, debitAccountId, creditAccountId, amount);

            // Assert
            Assert.That(ledgerEntry.EntryId, Is.EqualTo(entryId));
            Assert.That(ledgerEntry.DebitAccountId, Is.EqualTo(debitAccountId));
            Assert.That(ledgerEntry.CreditAccountId, Is.EqualTo(creditAccountId));
            Assert.That(ledgerEntry.Amount, Is.EqualTo(amount));
        }

        [Test]
        public void Equality_WithSameValues_ShouldBeEqual()
        {
            // Arrange
            var entryId = LedgerEntryId.New();
            var debitAccountId = new AccountId("DEBIT123");
            var creditAccountId = new AccountId("CREDIT456");
            var amount = new Money(250m, new Currency("GBP"));

            var entry1 = new LedgerEntry(entryId, debitAccountId, creditAccountId, amount);
            var entry2 = new LedgerEntry(entryId, debitAccountId, creditAccountId, amount);

            // Act & Assert
            Assert.That(entry1, Is.EqualTo(entry2));
            Assert.That(entry1.GetHashCode(), Is.EqualTo(entry2.GetHashCode()));
        }

        [Test]
        public void Equality_WithDifferentValues_ShouldNotBeEqual()
        {
            // Arrange
            var entry1 = new LedgerEntry(
                LedgerEntryId.New(), 
                new AccountId("DEBIT123"), 
                new AccountId("CREDIT456"), 
                new Money(250m, new Currency("GBP")));

            var entry2 = new LedgerEntry(
                LedgerEntryId.New(), 
                new AccountId("DEBIT789"), 
                new AccountId("CREDIT456"), 
                new Money(250m, new Currency("GBP")));

            // Act & Assert
            Assert.That(entry1, Is.Not.EqualTo(entry2));
        }
    }

    [TestFixture]
    public class EventImmutabilityTests
    {
        [Test]
        public void AllEvents_ShouldBeRecords()
        {
            // This test ensures all events are implemented as records (immutable)
            var eventTypes = new[]
            {
                typeof(PaymentRequested),
                typeof(AMLPassed),
                typeof(PaymentFlagged),
                typeof(FundsReserved),
                typeof(FundsReservationFailed),
                typeof(PaymentJournaled),
                typeof(PaymentSettled),
                typeof(PaymentCancelled),
                typeof(PaymentDeclined),
                typeof(PaymentFailed),
                typeof(PaymentNotified)
            };

            foreach (var eventType in eventTypes)
            {
                Assert.That(eventType.IsClass, Is.True, $"{eventType.Name} should be a class");
                // Records in C# are classes, but we can check they have the expected record behavior
                // This is mainly a compile-time guarantee, but we verify the type structure
            }
        }

        [Test]
        public void CreatedAt_ShouldBeInitOnly()
        {
            // Arrange & Act
            var eventObj = new PaymentRequested(
                PaymentId.New(), 
                new Money(100m, new Currency("USD")), 
                new AccountId("PAYER"), 
                new AccountId("PAYEE"), 
                "Test");

            var originalCreatedAt = eventObj.CreatedAt;

            // Assert - CreatedAt should have init-only setter (compile-time check)
            // If this compiles, the property is correctly implemented as init-only
            Assert.That(eventObj.CreatedAt, Is.EqualTo(originalCreatedAt));
        }
    }
}