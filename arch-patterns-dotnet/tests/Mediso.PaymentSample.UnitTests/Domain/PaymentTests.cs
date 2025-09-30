using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.Domain.Payments;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.Domain;

[TestFixture]
public class PaymentTests
{
    private PaymentId _paymentId;
    private Money _amount;
    private AccountId _payerAccountId;
    private AccountId _payeeAccountId;
    private string _reference;

    [SetUp]
    public void SetUp()
    {
        _paymentId = PaymentId.New();
        _amount = new Money(100m, new Currency("USD"));
        _payerAccountId = new AccountId("PAYER123");
        _payeeAccountId = new AccountId("PAYEE456");
        _reference = "Test payment";
    }

    [TestFixture]
    public class CreateTests : PaymentTests
    {
        [Test]
        public void Create_WithValidParameters_ShouldCreatePayment()
        {
            // Act
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Assert
            Assert.That(payment.Id, Is.EqualTo(_paymentId));
            Assert.That(payment.Amount, Is.EqualTo(_amount));
            Assert.That(payment.PayerAccountId, Is.EqualTo(_payerAccountId));
            Assert.That(payment.PayeeAccountId, Is.EqualTo(_payeeAccountId));
            Assert.That(payment.Reference, Is.EqualTo(_reference));
            Assert.That(payment.State, Is.EqualTo(PaymentState.Requested));
            Assert.That(payment.ReservationId, Is.Null);
        }

        [Test]
        public void Create_WithValidParameters_ShouldRaisePaymentRequestedEvent()
        {
            // Act
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Assert
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentRequested>());
            
            var paymentRequested = (PaymentRequested)domainEvent;
            Assert.That(paymentRequested.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentRequested.Amount, Is.EqualTo(_amount));
            Assert.That(paymentRequested.PayerAccountId, Is.EqualTo(_payerAccountId));
            Assert.That(paymentRequested.PayeeAccountId, Is.EqualTo(_payeeAccountId));
            Assert.That(paymentRequested.Reference, Is.EqualTo(_reference));
        }

        [Test]
        public void Create_WithSamePayerAndPayee_ShouldThrowDomainException()
        {
            // Arrange
            var sameAccountId = new AccountId("SAME123");

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => 
                Payment.Create(_paymentId, _amount, sameAccountId, sameAccountId, _reference));
            Assert.That(exception.Message, Is.EqualTo("Payer and Payee must differ"));
        }

        [Test]
        public void Create_WithZeroAmount_ShouldThrowDomainException()
        {
            // Arrange
            var zeroAmount = new Money(0m, new Currency("USD"));

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() =>
                Payment.Create(_paymentId, zeroAmount, _payerAccountId, _payeeAccountId, _reference));
            Assert.That(exception.Message, Is.EqualTo("Amount must be > 0"));
        }

        [Test]
        public void Create_WithNegativeAmount_ShouldThrowDomainException()
        {
            // Arrange
            var negativeAmount = new Money(-50m, new Currency("USD"));

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() =>
                Payment.Create(_paymentId, negativeAmount, _payerAccountId, _payeeAccountId, _reference));
            Assert.That(exception.Message, Is.EqualTo("Amount must be > 0"));
        }
    }

    [TestFixture]
    public class MarkAMLPassedTests : PaymentTests
    {
        [Test]
        public void MarkAMLPassed_FromRequestedState_ShouldChangeStateAndRaiseEvent()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var ruleSetVersion = "1.2.3";

            // Act
            payment.MarkAMLPassed(ruleSetVersion);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Requested)); // Stays in Requested for normal flow
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<AMLPassed>());
            var amlPassed = (AMLPassed)domainEvent;
            Assert.That(amlPassed.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(amlPassed.RuleSetVersion, Is.EqualTo(ruleSetVersion));
        }

        [Test]
        public void MarkAMLPassed_FromFlaggedState_ShouldChangeStateToReleased()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test reason", "Low");
            payment.ClearUncommittedEvents();
            var ruleSetVersion = "1.2.3";

            // Act
            payment.MarkAMLPassed(ruleSetVersion);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Released));
        }

        [Test]
        public void MarkAMLPassed_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ReserveFunds(ReservationId.New());

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.MarkAMLPassed("1.0"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Reserved"));
        }
    }

    [TestFixture]
    public class FlagTests : PaymentTests
    {
        [Test]
        public void Flag_FromRequestedState_ShouldChangeStateToFlagged()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var reason = "Suspicious amount";
            var severity = "High";

            // Act
            payment.Flag(reason, severity);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Flagged));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentFlagged>());
            var paymentFlagged = (PaymentFlagged)domainEvent;
            Assert.That(paymentFlagged.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentFlagged.Reason, Is.EqualTo(reason));
            Assert.That(paymentFlagged.Severity, Is.EqualTo(severity));
        }

        [Test]
        public void Flag_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test", "Low");

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Flag("Another reason", "High"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Flagged"));
        }
    }

    [TestFixture]
    public class ReleaseAfterFlagTests : PaymentTests
    {
        [Test]
        public void ReleaseAfterFlag_FromFlaggedState_ShouldChangeStateToReleased()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test reason", "Low");
            payment.ClearUncommittedEvents();

            // Act
            payment.ReleaseAfterFlag();

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Released));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<AMLPassed>());
            var amlPassed = (AMLPassed)domainEvent;
            Assert.That(amlPassed.RuleSetVersion, Is.EqualTo("manual-release"));
        }

        [Test]
        public void ReleaseAfterFlag_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.ReleaseAfterFlag());
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Requested"));
        }
    }

    [TestFixture]
    public class ReserveFundsTests : PaymentTests
    {
        [Test]
        public void ReserveFunds_FromRequestedState_ShouldChangeStateToReserved()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var reservationId = ReservationId.New();

            // Act
            payment.ReserveFunds(reservationId);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Reserved));
            Assert.That(payment.ReservationId, Is.EqualTo(reservationId));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<FundsReserved>());
            var fundsReserved = (FundsReserved)domainEvent;
            Assert.That(fundsReserved.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(fundsReserved.ReservationId, Is.EqualTo(reservationId));
            Assert.That(fundsReserved.Amount, Is.EqualTo(_amount));
        }

        [Test]
        public void ReserveFunds_FromReleasedState_ShouldChangeStateToReserved()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test", "Low");
            payment.ReleaseAfterFlag();
            payment.ClearUncommittedEvents();
            var reservationId = ReservationId.New();

            // Act
            payment.ReserveFunds(reservationId);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Reserved));
            Assert.That(payment.ReservationId, Is.EqualTo(reservationId));
        }

        [Test]
        public void ReserveFunds_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test", "Low");

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.ReserveFunds(ReservationId.New()));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Flagged"));
        }
    }

    [TestFixture]
    public class FailReservationTests : PaymentTests
    {
        [Test]
        public void FailReservation_FromValidState_ShouldRaiseEventButKeepState()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var reason = "Insufficient funds";

            // Act
            payment.FailReservation(reason);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Requested)); // State doesn't change
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<FundsReservationFailed>());
            var reservationFailed = (FundsReservationFailed)domainEvent;
            Assert.That(reservationFailed.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(reservationFailed.Reason, Is.EqualTo(reason));
        }

        [Test]
        public void FailReservation_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.Flag("Test", "Low");

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.FailReservation("Failed"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Flagged"));
        }
    }

    [TestFixture]
    public class JournalTests : PaymentTests
    {
        [Test]
        public void Journal_FromReservedState_ShouldChangeStateToJournaled()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ReserveFunds(ReservationId.New());
            payment.ClearUncommittedEvents();
            
            var entries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
            };

            // Act
            payment.Journal(entries);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Journaled));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentJournaled>());
            var paymentJournaled = (PaymentJournaled)domainEvent;
            Assert.That(paymentJournaled.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentJournaled.Entries, Is.EqualTo(entries));
        }

        [Test]
        public void Journal_WithEmptyEntries_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ReserveFunds(ReservationId.New());
            var emptyEntries = new List<LedgerEntry>();

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Journal(emptyEntries));
            Assert.That(exception.Message, Is.EqualTo("Journal requires entries"));
        }

        [Test]
        public void Journal_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            var entries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
            };

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Journal(entries));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Requested"));
        }
    }

    [TestFixture]
    public class SettleTests : PaymentTests
    {
        [Test]
        public void Settle_FromJournaledState_ShouldChangeStateToSettled()
        {
            // Arrange
            var payment = CreateJournaledPayment();
            payment.ClearUncommittedEvents();
            var channel = "SWIFT";
            var externalRef = "EXT123456";

            // Act
            payment.Settle(channel, externalRef);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Settled));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentSettled>());
            var paymentSettled = (PaymentSettled)domainEvent;
            Assert.That(paymentSettled.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentSettled.Channel, Is.EqualTo(channel));
            Assert.That(paymentSettled.ExternalRef, Is.EqualTo(externalRef));
        }

        [Test]
        public void Settle_WithNullExternalRef_ShouldWork()
        {
            // Arrange
            var payment = CreateJournaledPayment();
            payment.ClearUncommittedEvents();
            var channel = "INTERNAL";

            // Act
            payment.Settle(channel, null);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Settled));
            var domainEvent = payment.UncommittedEvents.First();
            var paymentSettled = (PaymentSettled)domainEvent;
            Assert.That(paymentSettled.ExternalRef, Is.Null);
        }

        [Test]
        public void Settle_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Settle("CHANNEL", "REF"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Requested"));
        }
    }

    [TestFixture]
    public class CancelTests : PaymentTests
    {
        [Test]
        public void Cancel_FromValidStates_ShouldChangeStateToDeclined()
        {
            // Test from Requested state
            var payment1 = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment1.ClearUncommittedEvents();

            payment1.Cancel("user123");
            Assert.That(payment1.State, Is.EqualTo(PaymentState.Declined));

            // Test from Flagged state
            var payment2 = Payment.Create(PaymentId.New(), _amount, _payerAccountId, _payeeAccountId, _reference);
            payment2.Flag("Test", "Low");
            payment2.ClearUncommittedEvents();

            payment2.Cancel("user123");
            Assert.That(payment2.State, Is.EqualTo(PaymentState.Declined));

            // Test from Released state
            var payment3 = Payment.Create(PaymentId.New(), _amount, _payerAccountId, _payeeAccountId, _reference);
            payment3.Flag("Test", "Low");
            payment3.ReleaseAfterFlag();
            payment3.ClearUncommittedEvents();

            payment3.Cancel("user123");
            Assert.That(payment3.State, Is.EqualTo(PaymentState.Declined));
        }

        [Test]
        public void Cancel_ShouldRaisePaymentCancelledEvent()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var by = "user123";

            // Act
            payment.Cancel(by);

            // Assert
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentCancelled>());
            var paymentCancelled = (PaymentCancelled)domainEvent;
            Assert.That(paymentCancelled.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentCancelled.By, Is.EqualTo(by));
        }

        [Test]
        public void Cancel_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = CreateJournaledPayment();

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Cancel("user"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Journaled"));
        }
    }

    [TestFixture]
    public class DeclineTests : PaymentTests
    {
        [Test]
        public void Decline_FromValidStates_ShouldChangeStateToDeclined()
        {
            // Test from various valid states
            var states = new[] { PaymentState.Requested, PaymentState.Flagged, PaymentState.Released, PaymentState.Reserved };
            
            foreach (var state in states)
            {
                var payment = CreatePaymentInState(state);
                payment.ClearUncommittedEvents();

                payment.Decline("Test reason");
                Assert.That(payment.State, Is.EqualTo(PaymentState.Declined));
            }
        }

        [Test]
        public void Decline_ShouldRaisePaymentDeclinedEvent()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var reason = "Insufficient funds";

            // Act
            payment.Decline(reason);

            // Assert
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentDeclined>());
            var paymentDeclined = (PaymentDeclined)domainEvent;
            Assert.That(paymentDeclined.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentDeclined.Reason, Is.EqualTo(reason));
        }

        [Test]
        public void Decline_FromInvalidState_ShouldThrowDomainException()
        {
            // Arrange
            var payment = CreateJournaledPayment();

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => payment.Decline("Reason"));
            Assert.That(exception.Message, Is.EqualTo("Operation not allowed in state Journaled"));
        }
    }

    [TestFixture]
    public class FailTests : PaymentTests
    {
        [Test]
        public void Fail_FromAnyState_ShouldChangeStateToFailed()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            payment.ClearUncommittedEvents();
            var reason = "Network timeout";

            // Act
            payment.Fail(reason);

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Failed));
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));
            
            var domainEvent = payment.UncommittedEvents.First();
            Assert.That(domainEvent, Is.InstanceOf<PaymentFailed>());
            var paymentFailed = (PaymentFailed)domainEvent;
            Assert.That(paymentFailed.PaymentId, Is.EqualTo(_paymentId));
            Assert.That(paymentFailed.Reason, Is.EqualTo(reason));
        }

        [Test]
        public void Fail_FromJournaledState_ShouldWork()
        {
            // Arrange
            var payment = CreateJournaledPayment();
            payment.ClearUncommittedEvents();

            // Act
            payment.Fail("Settlement failed");

            // Assert
            Assert.That(payment.State, Is.EqualTo(PaymentState.Failed));
        }
    }

    [TestFixture]
    public class AggregateTests : PaymentTests
    {
        [Test]
        public void Payment_ShouldInheritFromAggregate()
        {
            // Arrange & Act
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Assert
            Assert.That(payment, Is.InstanceOf<Aggregate<PaymentId>>());
            Assert.That(payment, Is.InstanceOf<IAggregateRoot>());
        }

        [Test]
        public void Payment_ShouldIncrementVersionOnEvents()
        {
            // Arrange & Act
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            var initialVersion = payment.Version;

            payment.Flag("Test", "Low");
            var afterFlagVersion = payment.Version;

            payment.ReleaseAfterFlag();
            var afterReleaseVersion = payment.Version;

            // Assert
            Assert.That(initialVersion, Is.EqualTo(1)); // First event (PaymentRequested)
            Assert.That(afterFlagVersion, Is.EqualTo(2)); // Second event (PaymentFlagged)
            Assert.That(afterReleaseVersion, Is.EqualTo(3)); // Third event (AMLPassed)
        }

        [Test]
        public void ClearUncommittedEvents_ShouldClearEventsList()
        {
            // Arrange
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(1));

            // Act
            payment.ClearUncommittedEvents();

            // Assert
            Assert.That(payment.UncommittedEvents.Count, Is.EqualTo(0));
        }

        [Test]
        public void UncommittedEvents_ShouldBeReadOnly()
        {
            // Arrange & Act
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);

            // Assert
            Assert.That(payment.UncommittedEvents, Is.InstanceOf<IReadOnlyCollection<IDomainEvent>>());
        }
    }

    [TestFixture]
    public class StateTransitionTests : PaymentTests
    {
        [Test]
        public void StateTransitions_ShouldFollowValidPaths()
        {
            // Test normal happy path: Requested -> Reserved -> Journaled -> Settled
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            Assert.That(payment.State, Is.EqualTo(PaymentState.Requested));

            payment.ReserveFunds(ReservationId.New());
            Assert.That(payment.State, Is.EqualTo(PaymentState.Reserved));

            var entries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
            };
            payment.Journal(entries);
            Assert.That(payment.State, Is.EqualTo(PaymentState.Journaled));

            payment.Settle("SWIFT", "EXT123");
            Assert.That(payment.State, Is.EqualTo(PaymentState.Settled));
        }

        [Test]
        public void StateTransitions_WithFlagging_ShouldFollowValidPaths()
        {
            // Test flagged path: Requested -> Flagged -> Released -> Reserved -> Journaled -> Settled
            var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
            
            payment.Flag("Suspicious", "High");
            Assert.That(payment.State, Is.EqualTo(PaymentState.Flagged));

            payment.ReleaseAfterFlag();
            Assert.That(payment.State, Is.EqualTo(PaymentState.Released));

            payment.ReserveFunds(ReservationId.New());
            Assert.That(payment.State, Is.EqualTo(PaymentState.Reserved));

            var entries = new List<LedgerEntry>
            {
                new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
            };
            payment.Journal(entries);
            Assert.That(payment.State, Is.EqualTo(PaymentState.Journaled));

            payment.Settle("SWIFT", "EXT123");
            Assert.That(payment.State, Is.EqualTo(PaymentState.Settled));
        }
    }

    // Helper methods
    private Payment CreateJournaledPayment()
    {
        var payment = Payment.Create(_paymentId, _amount, _payerAccountId, _payeeAccountId, _reference);
        payment.ReserveFunds(ReservationId.New());
        var entries = new List<LedgerEntry>
        {
            new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
        };
        payment.Journal(entries);
        return payment;
    }

    private Payment CreatePaymentInState(PaymentState targetState)
    {
        var payment = Payment.Create(PaymentId.New(), _amount, _payerAccountId, _payeeAccountId, _reference);
        
        switch (targetState)
        {
            case PaymentState.Requested:
                // Already in this state
                break;
            case PaymentState.Flagged:
                payment.Flag("Test", "Low");
                break;
            case PaymentState.Released:
                payment.Flag("Test", "Low");
                payment.ReleaseAfterFlag();
                break;
            case PaymentState.Reserved:
                payment.ReserveFunds(ReservationId.New());
                break;
            case PaymentState.Journaled:
                payment.ReserveFunds(ReservationId.New());
                var entries = new List<LedgerEntry>
                {
                    new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
                };
                payment.Journal(entries);
                break;
            case PaymentState.Settled:
                payment.ReserveFunds(ReservationId.New());
                var entries2 = new List<LedgerEntry>
                {
                    new(LedgerEntryId.New(), _payerAccountId, _payeeAccountId, _amount)
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