using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.SharedKernel;

[TestFixture]
public class DomainExceptionTests
{
    [Test]
    public void Constructor_WithMessage_ShouldCreateExceptionWithMessage()
    {
        // Arrange
        var message = "This is a domain exception";

        // Act
        var exception = new DomainException(message);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void Constructor_WithMessageAndInnerException_ShouldCreateExceptionWithBoth()
    {
        // Arrange
        var message = "This is a domain exception";
        var innerException = new ArgumentException("Inner exception");

        // Act
        var exception = new DomainException(message, innerException);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
    }

    [Test]
    public void Constructor_WithNullMessage_ShouldCreateExceptionWithDefaultMessage()
    {
        // Act
        var exception = new DomainException(null!);

        // Assert
        // When null is passed, the base Exception class provides a default message
        Assert.That(exception.Message, Is.Not.Null);
        Assert.That(exception.Message, Is.Not.Empty);
    }

    [Test]
    public void Constructor_WithEmptyMessage_ShouldCreateExceptionWithEmptyMessage()
    {
        // Arrange
        var message = string.Empty;

        // Act
        var exception = new DomainException(message);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void DomainException_ShouldInheritFromException()
    {
        // Arrange
        var exception = new DomainException("test message");

        // Act & Assert
        Assert.That(exception, Is.InstanceOf<Exception>());
    }

    [Test]
    public void DomainException_CanBeThrown()
    {
        // Arrange
        var message = "Test domain exception";

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => throw new DomainException(message));
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void DomainException_CanBeCaught()
    {
        // Arrange
        var message = "Test domain exception";

        // Act
        Exception? caughtException = null;
        try
        {
            throw new DomainException(message);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.That(caughtException, Is.Not.Null);
        Assert.That(caughtException, Is.InstanceOf<DomainException>());
        Assert.That(caughtException.Message, Is.EqualTo(message));
    }

    [Test]
    public void DomainException_WithInnerException_ShouldPreserveInnerExceptionDetails()
    {
        // Arrange
        var innerMessage = "Inner exception message";
        var innerException = new InvalidOperationException(innerMessage);
        var domainMessage = "Domain exception message";

        // Act
        var exception = new DomainException(domainMessage, innerException);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(domainMessage));
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
        Assert.That(exception.InnerException.Message, Is.EqualTo(innerMessage));
    }

    [TestCase("Validation failed")]
    [TestCase("Business rule violation")]
    [TestCase("Invalid state transition")]
    [TestCase("Aggregate invariant violated")]
    public void DomainException_WithVariousMessages_ShouldPreserveMessage(string message)
    {
        // Act
        var exception = new DomainException(message);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
    }

    [Test]
    public void DomainException_GetType_ShouldReturnCorrectType()
    {
        // Arrange
        var message = "Test domain exception";
        var exception = new DomainException(message);

        // Act
        var type = exception.GetType();

        // Assert
        Assert.That(type, Is.EqualTo(typeof(DomainException)));
        Assert.That(type.Name, Is.EqualTo(nameof(DomainException)));
    }

        [Test]
        public void DomainException_ToString_ShouldContainMessage()
        {
            // Arrange
            var message = "Test exception message";
            var exception = new DomainException(message);

            // Act
            var stringRepresentation = exception.ToString();

            // Assert
            Assert.That(stringRepresentation, Is.Not.Null);
            Assert.That(stringRepresentation, Is.Not.Empty);
            Assert.That(stringRepresentation, Does.Contain(message));
            Assert.That(stringRepresentation, Does.Contain(nameof(DomainException)));
        }
}