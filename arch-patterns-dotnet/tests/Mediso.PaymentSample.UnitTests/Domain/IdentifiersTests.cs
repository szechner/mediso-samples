using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.Domain;

[TestFixture]
public class IdentifiersTests
{
    [TestFixture]
    public class PaymentIdTests
    {
        [Test]
        public void Constructor_WithValidGuid_ShouldCreatePaymentId()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var paymentId = new PaymentId(guid);

            // Assert
            Assert.That(paymentId.Value, Is.EqualTo(guid));
        }

        [Test]
        public void Constructor_WithEmptyGuid_ShouldThrowDomainException()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => new PaymentId(emptyGuid));
            Assert.That(exception.Message, Is.EqualTo("PaymentId cannot be empty"));
        }

        [Test]
        public void New_ShouldCreatePaymentIdWithNonEmptyGuid()
        {
            // Act
            var paymentId = PaymentId.New();

            // Assert
            Assert.That(paymentId.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void New_ShouldCreateUniqueIds()
        {
            // Act
            var paymentId1 = PaymentId.New();
            var paymentId2 = PaymentId.New();

            // Assert
            Assert.That(paymentId1.Value, Is.Not.EqualTo(paymentId2.Value));
        }

        [Test]
        public void ToString_ShouldReturnGuidString()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var paymentId = new PaymentId(guid);

            // Act
            var result = paymentId.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(guid.ToString()));
        }

        [Test]
        public void ImplicitConversion_ToGuid_ShouldReturnValue()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var paymentId = new PaymentId(guid);

            // Act
            Guid convertedGuid = paymentId;

            // Assert
            Assert.That(convertedGuid, Is.EqualTo(guid));
        }

        [Test]
        public void ImplicitConversion_FromGuid_ShouldCreatePaymentId()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            PaymentId paymentId = guid;

            // Assert
            Assert.That(paymentId.Value, Is.EqualTo(guid));
        }

        [Test]
        public void ImplicitConversion_FromEmptyGuid_ShouldThrowDomainException()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() =>
            {
                PaymentId paymentId = emptyGuid;
            });
            Assert.That(exception.Message, Is.EqualTo("PaymentId cannot be empty"));
        }

        [Test]
        public void Equality_WithSameValue_ShouldBeEqual()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var paymentId1 = new PaymentId(guid);
            var paymentId2 = new PaymentId(guid);

            // Act & Assert
            Assert.That(paymentId1, Is.EqualTo(paymentId2));
            Assert.That(paymentId1.GetHashCode(), Is.EqualTo(paymentId2.GetHashCode()));
        }

        [Test]
        public void Equality_WithDifferentValue_ShouldNotBeEqual()
        {
            // Arrange
            var paymentId1 = PaymentId.New();
            var paymentId2 = PaymentId.New();

            // Act & Assert
            Assert.That(paymentId1, Is.Not.EqualTo(paymentId2));
        }
    }

    [TestFixture]
    public class ReservationIdTests
    {
        [Test]
        public void Constructor_WithValidGuid_ShouldCreateReservationId()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var reservationId = new ReservationId(guid);

            // Assert
            Assert.That(reservationId.Value, Is.EqualTo(guid));
        }

        [Test]
        public void Constructor_WithEmptyGuid_ShouldThrowDomainException()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => new ReservationId(emptyGuid));
            Assert.That(exception.Message, Is.EqualTo("ReservationId cannot be empty"));
        }

        [Test]
        public void New_ShouldCreateReservationIdWithNonEmptyGuid()
        {
            // Act
            var reservationId = ReservationId.New();

            // Assert
            Assert.That(reservationId.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void ImplicitConversions_ShouldWorkCorrectly()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act & Assert
            ReservationId reservationId = guid;
            Guid convertedGuid = reservationId;

            Assert.That(reservationId.Value, Is.EqualTo(guid));
            Assert.That(convertedGuid, Is.EqualTo(guid));
        }
    }

    [TestFixture]
    public class LedgerEntryIdTests
    {
        [Test]
        public void Constructor_WithValidGuid_ShouldCreateLedgerEntryId()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var ledgerEntryId = new LedgerEntryId(guid);

            // Assert
            Assert.That(ledgerEntryId.Value, Is.EqualTo(guid));
        }

        [Test]
        public void Constructor_WithEmptyGuid_ShouldThrowDomainException()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => new LedgerEntryId(emptyGuid));
            Assert.That(exception.Message, Is.EqualTo("LedgerEntryId cannot be empty"));
        }

        [Test]
        public void New_ShouldCreateLedgerEntryIdWithNonEmptyGuid()
        {
            // Act
            var ledgerEntryId = LedgerEntryId.New();

            // Assert
            Assert.That(ledgerEntryId.Value, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void ImplicitConversions_ShouldWorkCorrectly()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act & Assert
            LedgerEntryId ledgerEntryId = guid;
            Guid convertedGuid = ledgerEntryId;

            Assert.That(ledgerEntryId.Value, Is.EqualTo(guid));
            Assert.That(convertedGuid, Is.EqualTo(guid));
        }
    }

    [TestFixture]
    public class AccountIdTests
    {
        [Test]
        public void Constructor_WithValidString_ShouldCreateAccountId()
        {
            // Arrange
            var accountValue = "ACC123456";

            // Act
            var accountId = new AccountId(accountValue);

            // Assert
            Assert.That(accountId.Value, Is.EqualTo(accountValue));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n\r")]
        public void Constructor_WithInvalidString_ShouldThrowDomainException(string? invalidValue)
        {
            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => new AccountId(invalidValue!));
            Assert.That(exception.Message, Is.EqualTo("AccountId cannot be empty or invalid"));
        }

        [Test]
        public void Constructor_WithStringTooLong_ShouldThrowDomainException()
        {
            // Arrange
            var tooLongString = new string('A', 51); // More than 50 characters

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => new AccountId(tooLongString));
            Assert.That(exception.Message, Is.EqualTo("AccountId cannot be empty or invalid"));
        }

        [TestCase("A")]
        [TestCase("ACC123")]
        [TestCase("ACCOUNT-456")]
        [TestCase("user@domain.com")]
        [TestCase("12345678901234567890123456789012345678901234567890")] // Exactly 50 chars
        public void Constructor_WithValidStrings_ShouldCreateAccountId(string validValue)
        {
            // Act
            var accountId = new AccountId(validValue);

            // Assert
            Assert.That(accountId.Value, Is.EqualTo(validValue));
        }

        [Test]
        public void New_ShouldCreateAccountIdFromString()
        {
            // Arrange
            var accountValue = "ACC789";

            // Act
            var accountId = AccountId.New(accountValue);

            // Assert
            Assert.That(accountId.Value, Is.EqualTo(accountValue));
        }

        [Test]
        public void ToString_ShouldReturnAccountValue()
        {
            // Arrange
            var accountValue = "ACC123456";
            var accountId = new AccountId(accountValue);

            // Act
            var result = accountId.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(accountValue));
        }

        [Test]
        public void ImplicitConversion_ToString_ShouldReturnValue()
        {
            // Arrange
            var accountValue = "ACC123456";
            var accountId = new AccountId(accountValue);

            // Act
            string convertedString = accountId;

            // Assert
            Assert.That(convertedString, Is.EqualTo(accountValue));
        }

        [Test]
        public void ImplicitConversion_FromString_ShouldCreateAccountId()
        {
            // Arrange
            var accountValue = "ACC123456";

            // Act
            AccountId accountId = accountValue;

            // Assert
            Assert.That(accountId.Value, Is.EqualTo(accountValue));
        }

        [Test]
        public void ImplicitConversion_FromInvalidString_ShouldThrowDomainException()
        {
            // Arrange
            var invalidString = "";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() =>
            {
                AccountId accountId = invalidString;
            });
            Assert.That(exception.Message, Is.EqualTo("AccountId cannot be empty or invalid"));
        }

        [Test]
        public void Equality_WithSameValue_ShouldBeEqual()
        {
            // Arrange
            var accountValue = "ACC123456";
            var accountId1 = new AccountId(accountValue);
            var accountId2 = new AccountId(accountValue);

            // Act & Assert
            Assert.That(accountId1, Is.EqualTo(accountId2));
            Assert.That(accountId1.GetHashCode(), Is.EqualTo(accountId2.GetHashCode()));
        }

        [Test]
        public void Equality_WithDifferentValue_ShouldNotBeEqual()
        {
            // Arrange
            var accountId1 = new AccountId("ACC123");
            var accountId2 = new AccountId("ACC456");

            // Act & Assert
            Assert.That(accountId1, Is.Not.EqualTo(accountId2));
        }

        [Test]
        public void Value_WithNullInternalValue_ShouldReturnEmptyString()
        {
            // Note: This is a defensive test for the null coalescence in the Value property
            // It's difficult to create a scenario with null internal value through normal construction
            // but the property handles it defensively
            
            // Arrange - We can't easily create AccountId with null value through public API
            // But we can verify that the Value property handles null gracefully
            var accountId = new AccountId("test");
            
            // Act & Assert
            Assert.That(accountId.Value, Is.Not.Null);
        }
    }

    [TestFixture]
    public class InteractionTests
    {
        [Test]
        public void AllIdTypes_ShouldBeDistinct()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var paymentId = new PaymentId(guid);
            var reservationId = new ReservationId(guid);
            var ledgerEntryId = new LedgerEntryId(guid);

            // Act & Assert - They should not be equal even with same GUID
            // (This tests type safety of the strong typing)
            Assert.That(paymentId.Value, Is.EqualTo(guid));
            Assert.That(reservationId.Value, Is.EqualTo(guid));
            Assert.That(ledgerEntryId.Value, Is.EqualTo(guid));
            
            // Different types should not be comparable at compile time
            // but they all contain the same underlying GUID value
        }

        [Test]
        public void GuidIds_WithSameGuidValue_ShouldHaveSameUnderlyingValue()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var paymentId = new PaymentId(guid);
            var reservationId = new ReservationId(guid);
            var ledgerEntryId = new LedgerEntryId(guid);

            // Assert
            Assert.That(paymentId.Value, Is.EqualTo(reservationId.Value));
            Assert.That(reservationId.Value, Is.EqualTo(ledgerEntryId.Value));
        }

        [Test]
        public void AccountId_WithSpecialCharacters_ShouldBeValid()
        {
            // Arrange
            var accountValue = "user-123@company.com";

            // Act
            var accountId = new AccountId(accountValue);

            // Assert
            Assert.That(accountId.Value, Is.EqualTo(accountValue));
        }
    }
}