using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.SharedKernel;

[TestFixture]
public class ValueObjectsTests
{
    [TestFixture]
    public class CurrencyTests
    {
        [Test]
        public void Constructor_WithValidCode_ShouldCreateCurrency()
        {
            // Arrange
            var currencyCode = "USD";

            // Act
            var currency = new Currency(currencyCode);

            // Assert
            Assert.That(currency.Code, Is.EqualTo(currencyCode));
        }

        [TestCase("USD")]
        [TestCase("EUR")]
        [TestCase("GBP")]
        [TestCase("JPY")]
        [TestCase("CZK")]
        public void Constructor_WithVariousCurrencyCodes_ShouldCreateCurrency(string currencyCode)
        {
            // Act
            var currency = new Currency(currencyCode);

            // Assert
            Assert.That(currency.Code, Is.EqualTo(currencyCode));
        }

        [Test]
        public void ToString_ShouldReturnCurrencyCode()
        {
            // Arrange
            var currencyCode = "EUR";
            var currency = new Currency(currencyCode);

            // Act
            var result = currency.ToString();

            // Assert
            Assert.That(result, Is.EqualTo(currencyCode));
        }

        [Test]
        public void Equality_WithSameCurrencyCode_ShouldBeEqual()
        {
            // Arrange
            var currency1 = new Currency("USD");
            var currency2 = new Currency("USD");

            // Act & Assert
            Assert.That(currency1, Is.EqualTo(currency2));
            Assert.That(currency1.GetHashCode(), Is.EqualTo(currency2.GetHashCode()));
        }

        [Test]
        public void Equality_WithDifferentCurrencyCode_ShouldNotBeEqual()
        {
            // Arrange
            var currency1 = new Currency("USD");
            var currency2 = new Currency("EUR");

            // Act & Assert
            Assert.That(currency1, Is.Not.EqualTo(currency2));
        }

        [Test]
        public void Constructor_WithNullCode_ShouldCreateCurrencyWithNullCode()
        {
            // Arrange
            string? nullCode = null;

            // Act
            var currency = new Currency(nullCode!);

            // Assert
            Assert.That(currency.Code, Is.Null);
        }

        [Test]
        public void Constructor_WithEmptyCode_ShouldCreateCurrencyWithEmptyCode()
        {
            // Arrange
            var emptyCode = string.Empty;

            // Act
            var currency = new Currency(emptyCode);

            // Assert
            Assert.That(currency.Code, Is.EqualTo(emptyCode));
        }
    }

    [TestFixture]
    public class MoneyTests
    {
        [Test]
        public void Constructor_WithValidAmountAndCurrency_ShouldCreateMoney()
        {
            // Arrange
            var amount = 100.50m;
            var currency = new Currency("USD");

            // Act
            var money = new Money(amount, currency);

            // Assert
            Assert.That(money.Amount, Is.EqualTo(amount));
            Assert.That(money.Currency, Is.EqualTo(currency));
        }

        [TestCase(0.01)]
        [TestCase(1.0)]
        [TestCase(100.99)]
        [TestCase(1000000.00)]
        public void Constructor_WithVariousAmounts_ShouldCreateMoney(decimal amount)
        {
            // Arrange
            var currency = new Currency("EUR");

            // Act
            var money = new Money(amount, currency);

            // Assert
            Assert.That(money.Amount, Is.EqualTo(amount));
            Assert.That(money.Currency, Is.EqualTo(currency));
        }

        [Test]
        public void EnsurePositive_WithPositiveAmount_ShouldReturnSameMoney()
        {
            // Arrange
            var money = new Money(100.50m, new Currency("USD"));

            // Act
            var result = money.EnsurePositive();

            // Assert
            Assert.That(result, Is.EqualTo(money));
        }

        [Test]
        public void EnsurePositive_WithZeroAmount_ShouldThrowDomainException()
        {
            // Arrange
            var money = new Money(0m, new Currency("USD"));

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => money.EnsurePositive());
            Assert.That(exception.Message, Is.EqualTo("Amount must be > 0"));
        }

        [Test]
        public void EnsurePositive_WithNegativeAmount_ShouldThrowDomainException()
        {
            // Arrange
            var money = new Money(-10.50m, new Currency("USD"));

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => money.EnsurePositive());
            Assert.That(exception.Message, Is.EqualTo("Amount must be > 0"));
        }

        [TestCase(-0.01)]
        [TestCase(-1.0)]
        [TestCase(-100.50)]
        [TestCase(-1000000.00)]
        public void EnsurePositive_WithVariousNegativeAmounts_ShouldThrowDomainException(decimal negativeAmount)
        {
            // Arrange
            var money = new Money(negativeAmount, new Currency("EUR"));

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => money.EnsurePositive());
            Assert.That(exception.Message, Is.EqualTo("Amount must be > 0"));
        }

        [Test]
        public void Equality_WithSameAmountAndCurrency_ShouldBeEqual()
        {
            // Arrange
            var money1 = new Money(100.50m, new Currency("USD"));
            var money2 = new Money(100.50m, new Currency("USD"));

            // Act & Assert
            Assert.That(money1, Is.EqualTo(money2));
            Assert.That(money1.GetHashCode(), Is.EqualTo(money2.GetHashCode()));
        }

        [Test]
        public void Equality_WithDifferentAmount_ShouldNotBeEqual()
        {
            // Arrange
            var money1 = new Money(100.50m, new Currency("USD"));
            var money2 = new Money(200.50m, new Currency("USD"));

            // Act & Assert
            Assert.That(money1, Is.Not.EqualTo(money2));
        }

        [Test]
        public void Equality_WithDifferentCurrency_ShouldNotBeEqual()
        {
            // Arrange
            var money1 = new Money(100.50m, new Currency("USD"));
            var money2 = new Money(100.50m, new Currency("EUR"));

            // Act & Assert
            Assert.That(money1, Is.Not.EqualTo(money2));
        }

        [Test]
        public void Equality_WithDifferentAmountAndCurrency_ShouldNotBeEqual()
        {
            // Arrange
            var money1 = new Money(100.50m, new Currency("USD"));
            var money2 = new Money(200.75m, new Currency("EUR"));

            // Act & Assert
            Assert.That(money1, Is.Not.EqualTo(money2));
        }

        [Test]
        public void Constructor_WithMaxDecimalValue_ShouldCreateMoney()
        {
            // Arrange
            var maxAmount = decimal.MaxValue;
            var currency = new Currency("USD");

            // Act
            var money = new Money(maxAmount, currency);

            // Assert
            Assert.That(money.Amount, Is.EqualTo(maxAmount));
            Assert.That(money.Currency, Is.EqualTo(currency));
        }

        [Test]
        public void Constructor_WithMinDecimalValue_ShouldCreateMoney()
        {
            // Arrange
            var minAmount = decimal.MinValue;
            var currency = new Currency("USD");

            // Act
            var money = new Money(minAmount, currency);

            // Assert
            Assert.That(money.Amount, Is.EqualTo(minAmount));
            Assert.That(money.Currency, Is.EqualTo(currency));
        }

        [Test]
        public void EnsurePositive_WithVerySmallPositiveAmount_ShouldReturnSameMoney()
        {
            // Arrange
            var smallAmount = 0.0000000000000000001m; // Very small positive amount
            var money = new Money(smallAmount, new Currency("USD"));

            // Act
            var result = money.EnsurePositive();

            // Assert
            Assert.That(result, Is.EqualTo(money));
        }

        [Test]
        public void ToString_ShouldReturnReadableFormat()
        {
            // Arrange
            var money = new Money(100.50m, new Currency("USD"));

            // Act
            var result = money.ToString();

            // Assert
            // Note: ToString() behavior depends on the default record implementation
            // This test ensures it doesn't throw and returns some string representation
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Not.Empty);
            Assert.That(result, Does.Contain("100,50")); // Czech locale uses comma as decimal separator
            Assert.That(result, Does.Contain("USD"));
        }
    }

    [TestFixture]
    public class InteractionTests
    {
        [Test]
        public void Money_WithMultipleCurrencies_ShouldMaintainDistinction()
        {
            // Arrange
            var usdCurrency = new Currency("USD");
            var eurCurrency = new Currency("EUR");
            var amount = 100m;

            var usdMoney = new Money(amount, usdCurrency);
            var eurMoney = new Money(amount, eurCurrency);

            // Act & Assert
            Assert.That(usdMoney.Currency.Code, Is.EqualTo("USD"));
            Assert.That(eurMoney.Currency.Code, Is.EqualTo("EUR"));
            Assert.That(usdMoney, Is.Not.EqualTo(eurMoney));
        }

        [Test]
        public void Money_EnsurePositive_ShouldNotModifyOriginal()
        {
            // Arrange
            var originalMoney = new Money(100.50m, new Currency("USD"));

            // Act
            var checkedMoney = originalMoney.EnsurePositive();

            // Assert
            Assert.That(checkedMoney, Is.EqualTo(originalMoney));
            // Verify original is unchanged (should be the same since it's a value object)
            Assert.That(originalMoney.Amount, Is.EqualTo(100.50m));
            Assert.That(originalMoney.Currency.Code, Is.EqualTo("USD"));
        }
    }
}