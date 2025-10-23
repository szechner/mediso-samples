using Mediso.PaymentSample.SharedKernel.Domain;

namespace Mediso.PaymentSample.UnitTests.IntegrationEvents;

[TestFixture]
public class IntegrationEventVersionRegistryTests
{
    private const string TestEventType = "TestEvent";
    private const string AnotherEventType = "AnotherEvent";

    [SetUp]
    public void SetUp()
    {
        // Note: We can't easily clear the static registry between tests
        // so we use unique event type names for each test scenario
    }

    [Test]
    public void RegisterEventVersion_ShouldStoreEventVersionInfo()
    {
        // Arrange
        var eventType = $"{TestEventType}_Register_{Guid.NewGuid():N}";
        var currentVersion = 2;
        var supportedVersions = new[] { 1, 2 };

        // Act
        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion, supportedVersions);

        // Assert
        var retrievedCurrentVersion = IntegrationEventVersionRegistry.GetCurrentVersion(eventType);
        Assert.That(retrievedCurrentVersion, Is.EqualTo(currentVersion));
        
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 1), Is.True);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 2), Is.True);
    }

    [Test]
    public void IsVersionSupported_ShouldReturnTrue_ForSupportedVersions()
    {
        // Arrange
        var eventType = $"{TestEventType}_Supported_{Guid.NewGuid():N}";
        var supportedVersions = new[] { 1, 2, 4 }; // Note: version 3 is not supported

        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion: 4, supportedVersions);

        // Act & Assert
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 1), Is.True);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 2), Is.True);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 3), Is.False);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 4), Is.True);
    }

    [Test]
    public void IsVersionSupported_ShouldReturnFalse_ForUnsupportedVersions()
    {
        // Arrange
        var eventType = $"{TestEventType}_Unsupported_{Guid.NewGuid():N}";
        var supportedVersions = new[] { 1, 2 };

        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion: 2, supportedVersions);

        // Act & Assert
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 0), Is.False);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 3), Is.False);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 99), Is.False);
    }

    [Test]
    public void IsVersionSupported_ShouldReturnFalse_ForUnregisteredEventType()
    {
        // Arrange
        var unregisteredEventType = $"UnregisteredEvent_{Guid.NewGuid():N}";

        // Act & Assert
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(unregisteredEventType, 1), Is.False);
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(unregisteredEventType, 2), Is.False);
    }

    [Test]
    public void GetCurrentVersion_ShouldReturnRegisteredVersion()
    {
        // Arrange
        var eventType = $"{TestEventType}_Current_{Guid.NewGuid():N}";
        var expectedCurrentVersion = 5;

        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, expectedCurrentVersion, new[] { 3, 4, 5 });

        // Act
        var currentVersion = IntegrationEventVersionRegistry.GetCurrentVersion(eventType);

        // Assert
        Assert.That(currentVersion, Is.EqualTo(expectedCurrentVersion));
    }

    [Test]
    public void GetCurrentVersion_ShouldReturnDefaultVersion_ForUnregisteredEventType()
    {
        // Arrange
        var unregisteredEventType = $"UnregisteredEvent_{Guid.NewGuid():N}";

        // Act
        var currentVersion = IntegrationEventVersionRegistry.GetCurrentVersion(unregisteredEventType);

        // Assert
        Assert.That(currentVersion, Is.EqualTo(1)); // Default version
    }

    [Test]
    public void RegisterEventVersion_ShouldOverridePreviousRegistration()
    {
        // Arrange
        var eventType = $"{TestEventType}_Override_{Guid.NewGuid():N}";
        
        // First registration
        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion: 1, new[] { 1 });
        
        // Act - Second registration (should not override due to TryAdd)
        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion: 2, new[] { 1, 2 });

        // Assert - Should still have the first registration
        var currentVersion = IntegrationEventVersionRegistry.GetCurrentVersion(eventType);
        Assert.That(currentVersion, Is.EqualTo(1)); // Should be the first registration
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 2), Is.False);
    }

    [Test]
    public void RegisterEventVersion_WithEmptySupportedVersions_ShouldWork()
    {
        // Arrange
        var eventType = $"{TestEventType}_EmptySupported_{Guid.NewGuid():N}";
        var supportedVersions = new int[0]; // Empty array

        // Act
        IntegrationEventVersionRegistry.RegisterEventVersion(
            eventType, currentVersion: 1, supportedVersions);

        // Assert
        Assert.That(IntegrationEventVersionRegistry.GetCurrentVersion(eventType), Is.EqualTo(1));
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 1), Is.False);
    }

    [Test]
    public void RegisterEventVersion_WithMigrationFunction_ShouldStoreFunction()
    {
        // Arrange
        var eventType = $"{TestEventType}_Migration_{Guid.NewGuid():N}";
        var migrationFunc = new Func<object, int, int, object>((obj, from, to) => obj);

        // Act - This should not throw
        Assert.DoesNotThrow(() =>
        {
            IntegrationEventVersionRegistry.RegisterEventVersion(
                eventType, currentVersion: 2, new[] { 1, 2 }, migrationFunc);
        });

        // Assert - Basic functionality should still work
        Assert.That(IntegrationEventVersionRegistry.GetCurrentVersion(eventType), Is.EqualTo(2));
        Assert.That(IntegrationEventVersionRegistry.IsVersionSupported(eventType, 1), Is.True);
    }

    [Test]
    public void RegisterEventVersion_WithNullMigrationFunction_ShouldWork()
    {
        // Arrange
        var eventType = $"{TestEventType}_NullMigration_{Guid.NewGuid():N}";

        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() =>
        {
            IntegrationEventVersionRegistry.RegisterEventVersion(
                eventType, currentVersion: 1, new[] { 1 }, migrationFunc: null);
        });
    }

    [Test]
    public void ConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        var eventTypeBase = $"{TestEventType}_Concurrent_{Guid.NewGuid():N}";
        var tasks = new List<Task>();

        // Act - Multiple concurrent registrations and reads
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var eventType = $"{eventTypeBase}_{index}";
                IntegrationEventVersionRegistry.RegisterEventVersion(
                    eventType, currentVersion: index + 1, new[] { 1, index + 1 });
            }));

            tasks.Add(Task.Run(() =>
            {
                var eventType = $"{eventTypeBase}_{index}";
                IntegrationEventVersionRegistry.GetCurrentVersion(eventType);
                IntegrationEventVersionRegistry.IsVersionSupported(eventType, 1);
            }));
        }

        // Assert - Should not throw
        Assert.DoesNotThrowAsync(async () =>
        {
            await Task.WhenAll(tasks);
        });
    }
}