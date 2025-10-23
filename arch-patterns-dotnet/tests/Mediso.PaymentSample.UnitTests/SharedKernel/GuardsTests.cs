using Mediso.PaymentSample.SharedKernel;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.SharedKernel;

[TestFixture]
public class GuardsTests
{
    [TestFixture]
    public class NotEmptyTests
    {
        [Test]
        public void NotEmpty_WithValidGuid_ShouldReturnSameGuid()
        {
            // Arrange
            var validGuid = Guid.NewGuid();

            // Act
            var result = Guards.NotEmpty(validGuid);

            // Assert
            Assert.That(result, Is.EqualTo(validGuid));
        }

        [Test]
        public void NotEmpty_WithEmptyGuid_ShouldThrowDomainException()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotEmpty(emptyGuid));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotEmpty_WithEmptyGuidAndCustomParamName_ShouldThrowDomainExceptionWithCustomMessage()
        {
            // Arrange
            var emptyGuid = Guid.Empty;
            var customParamName = "customParameter";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotEmpty(emptyGuid, customParamName));
            Assert.That(exception.Message, Is.EqualTo($"{customParamName} must not be empty"));
        }

        [Test]
        public void NotEmpty_WithNullParamName_ShouldUseDefaultParamName()
        {
            // Arrange
            var emptyGuid = Guid.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotEmpty(emptyGuid, null));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotEmpty_WithRandomGuids_ShouldReturnSameValues()
        {
            // Arrange
            var guids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

            // Act & Assert
            foreach (var guid in guids)
            {
                var result = Guards.NotEmpty(guid);
                Assert.That(result, Is.EqualTo(guid));
            }
        }
    }

    [TestFixture]
    public class NotNullOrWhiteSpaceTests
    {
        [Test]
        public void NotNullOrWhiteSpace_WithValidString_ShouldReturnSameString()
        {
            // Arrange
            var validString = "Valid String";

            // Act
            var result = Guards.NotNullOrWhiteSpace(validString);

            // Assert
            Assert.That(result, Is.EqualTo(validString));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithNullString_ShouldThrowDomainException()
        {
            // Arrange
            string? nullString = null;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(nullString));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithEmptyString_ShouldThrowDomainException()
        {
            // Arrange
            var emptyString = string.Empty;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(emptyString));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithWhitespaceString_ShouldThrowDomainException()
        {
            // Arrange
            var whitespaceString = "   ";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(whitespaceString));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithTabsAndSpaces_ShouldThrowDomainException()
        {
            // Arrange
            var tabsAndSpaces = "\t  \n  \r";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(tabsAndSpaces));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithCustomParamName_ShouldThrowDomainExceptionWithCustomMessage()
        {
            // Arrange
            string? nullString = null;
            var customParamName = "customParameter";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(nullString, customParamName));
            Assert.That(exception.Message, Is.EqualTo($"{customParamName} must not be empty"));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithNullParamName_ShouldUseDefaultParamName()
        {
            // Arrange
            string? nullString = null;

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(nullString, null));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }

        [TestCase("a")]
        [TestCase("Valid")]
        [TestCase("Valid String with spaces")]
        [TestCase("123")]
        [TestCase("Special!@#$%")]
        [TestCase("Unicode: ñéáíóú")]
        public void NotNullOrWhiteSpace_WithValidStrings_ShouldReturnSameString(string validString)
        {
            // Act
            var result = Guards.NotNullOrWhiteSpace(validString);

            // Assert
            Assert.That(result, Is.EqualTo(validString));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithStringContainingLeadingAndTrailingSpaces_ShouldReturnSameString()
        {
            // Arrange - Note: Leading/trailing spaces with content are valid
            var stringWithSpaces = " Valid Content ";

            // Act
            var result = Guards.NotNullOrWhiteSpace(stringWithSpaces);

            // Assert
            Assert.That(result, Is.EqualTo(stringWithSpaces));
        }

        [Test]
        public void NotNullOrWhiteSpace_WithStringContainingOnlyNewlines_ShouldThrowDomainException()
        {
            // Arrange
            var newlineString = "\n\r\n";

            // Act & Assert
            var exception = Assert.Throws<DomainException>(() => Guards.NotNullOrWhiteSpace(newlineString));
            Assert.That(exception.Message, Is.EqualTo("value must not be empty"));
        }
    }

    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        public void NotEmpty_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            var validGuid = Guid.NewGuid();
            const int iterations = 1000000;

            // Act & Assert - Should complete quickly without throwing
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    Guards.NotEmpty(validGuid);
                }
            });
        }

        [Test]
        public void NotNullOrWhiteSpace_PerformanceTest_ShouldBeEfficient()
        {
            // Arrange
            var validString = "Valid test string";
            const int iterations = 1000000;

            // Act & Assert - Should complete quickly without throwing
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    Guards.NotNullOrWhiteSpace(validString);
                }
            });
        }
    }
}