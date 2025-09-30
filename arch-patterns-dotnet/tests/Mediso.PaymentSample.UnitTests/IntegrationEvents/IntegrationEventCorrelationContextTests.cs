using Mediso.PaymentSample.Domain;
using Mediso.PaymentSample.Domain.Common;
using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.IntegrationEvents;

[TestFixture]
public class IntegrationEventCorrelationContextTests
{
    [SetUp]
    public void SetUp()
    {
        // Ensure clean state before each test
        IntegrationEventCorrelationContext.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up after each test
        IntegrationEventCorrelationContext.Clear();
    }

    [Test]
    public void Current_ShouldReturnNull_WhenNotSet()
    {
        // Act
        var current = IntegrationEventCorrelationContext.Current;

        // Assert
        Assert.That(current, Is.Null);
    }

    [Test]
    public void Set_ShouldSetCurrentCorrelationId()
    {
        // Arrange
        var correlationId = "TEST-CORRELATION-123";

        // Act
        IntegrationEventCorrelationContext.Set(correlationId);
        var current = IntegrationEventCorrelationContext.Current;

        // Assert
        Assert.That(current, Is.EqualTo(correlationId));
    }

    [Test]
    public void Clear_ShouldClearCurrentCorrelationId()
    {
        // Arrange
        IntegrationEventCorrelationContext.Set("TEST-CORRELATION-123");

        // Act
        IntegrationEventCorrelationContext.Clear();
        var current = IntegrationEventCorrelationContext.Current;

        // Assert
        Assert.That(current, Is.Null);
    }

    [Test]
    public void Set_ShouldOverridePreviousValue()
    {
        // Arrange
        var firstCorrelationId = "FIRST-CORRELATION-123";
        var secondCorrelationId = "SECOND-CORRELATION-456";

        // Act
        IntegrationEventCorrelationContext.Set(firstCorrelationId);
        IntegrationEventCorrelationContext.Set(secondCorrelationId);
        var current = IntegrationEventCorrelationContext.Current;

        // Assert
        Assert.That(current, Is.EqualTo(secondCorrelationId));
    }

    [Test]
    public void CreateScope_ShouldSetCorrelationIdInScope()
    {
        // Arrange
        var correlationId = "SCOPED-CORRELATION-123";

        // Act & Assert
        using (var scope = IntegrationEventCorrelationContext.CreateScope(correlationId))
        {
            var current = IntegrationEventCorrelationContext.Current;
            Assert.That(current, Is.EqualTo(correlationId));
        }
    }

    [Test]
    public void CreateScope_ShouldRestorePreviousValueAfterDispose()
    {
        // Arrange
        var originalCorrelationId = "ORIGINAL-CORRELATION-123";
        var scopedCorrelationId = "SCOPED-CORRELATION-456";
        
        IntegrationEventCorrelationContext.Set(originalCorrelationId);

        // Act
        using (var scope = IntegrationEventCorrelationContext.CreateScope(scopedCorrelationId))
        {
            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(scopedCorrelationId));
        }

        // Assert
        var currentAfterScope = IntegrationEventCorrelationContext.Current;
        Assert.That(currentAfterScope, Is.EqualTo(originalCorrelationId));
    }

    [Test]
    public void CreateScope_ShouldClearWhenNoPreviousValue()
    {
        // Arrange
        var scopedCorrelationId = "SCOPED-CORRELATION-123";

        // Act
        using (var scope = IntegrationEventCorrelationContext.CreateScope(scopedCorrelationId))
        {
            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(scopedCorrelationId));
        }

        // Assert
        var currentAfterScope = IntegrationEventCorrelationContext.Current;
        Assert.That(currentAfterScope, Is.Null);
    }

    [Test]
    public void CreateScope_WithIntegrationEvent_ShouldUseEventCorrelationId()
    {
        // Arrange
        var expectedCorrelationId = "EVENT-CORRELATION-123";
        var integrationEvent = new AMLScreeningCompletedIntegrationEvent(
            PaymentId.New(), true, "v1.0")
        {
            CorrelationId = expectedCorrelationId
        };

        // Act & Assert
        using (var scope = IntegrationEventCorrelationContext.CreateScope(integrationEvent))
        {
            var current = IntegrationEventCorrelationContext.Current;
            Assert.That(current, Is.EqualTo(expectedCorrelationId));
        }
    }

    [Test]
    public void NestedScopes_ShouldWorkCorrectly()
    {
        // Arrange
        var outerCorrelationId = "OUTER-CORRELATION-123";
        var innerCorrelationId = "INNER-CORRELATION-456";

        // Act & Assert
        using (var outerScope = IntegrationEventCorrelationContext.CreateScope(outerCorrelationId))
        {
            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(outerCorrelationId));

            using (var innerScope = IntegrationEventCorrelationContext.CreateScope(innerCorrelationId))
            {
                Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(innerCorrelationId));
            }

            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(outerCorrelationId));
        }

        Assert.That(IntegrationEventCorrelationContext.Current, Is.Null);
    }

    [Test]
    public async Task AsyncContext_ShouldMaintainCorrelationAcrossAsyncCalls()
    {
        // Arrange
        var correlationId = "ASYNC-CORRELATION-123";

        // Act & Assert
        using (var scope = IntegrationEventCorrelationContext.CreateScope(correlationId))
        {
            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(correlationId));

            await Task.Run(() =>
            {
                Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(correlationId));
            });

            await Task.Delay(10);
            Assert.That(IntegrationEventCorrelationContext.Current, Is.EqualTo(correlationId));
        }

        Assert.That(IntegrationEventCorrelationContext.Current, Is.Null);
    }

    [Test]
    public async Task ParallelTasks_ShouldHaveIndependentCorrelationContexts()
    {
        // Arrange
        var correlationId1 = "PARALLEL-CORRELATION-1";
        var correlationId2 = "PARALLEL-CORRELATION-2";
        var results = new string[2];

        // Act
        var task1 = Task.Run(() =>
        {
            using (IntegrationEventCorrelationContext.CreateScope(correlationId1))
            {
                Thread.Sleep(50); // Simulate some work
                results[0] = IntegrationEventCorrelationContext.Current!;
            }
        });

        var task2 = Task.Run(() =>
        {
            using (IntegrationEventCorrelationContext.CreateScope(correlationId2))
            {
                Thread.Sleep(50); // Simulate some work
                results[1] = IntegrationEventCorrelationContext.Current!;
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert
        Assert.That(results[0], Is.EqualTo(correlationId1));
        Assert.That(results[1], Is.EqualTo(correlationId2));
    }

    [Test]
    public void ScopeDispose_ShouldBeIdempotent()
    {
        // Arrange
        var correlationId = "DISPOSE-TEST-123";
        var scope = IntegrationEventCorrelationContext.CreateScope(correlationId);

        // Act
        scope.Dispose();
        scope.Dispose(); // Second dispose should not throw

        // Assert
        Assert.That(IntegrationEventCorrelationContext.Current, Is.Null);
    }
}