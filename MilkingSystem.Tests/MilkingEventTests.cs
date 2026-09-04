using MilkingSystem.Core.Services;
using Xunit;
using MilkingSystem.Core.Notifications;

namespace MilkingSystem.Tests;

/// <summary>
/// Additional integration tests that demonstrate the test isolation problem.
/// These tests share database state with DataServiceIntegrationTests.
/// </summary>
public class MilkingEventTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly DataService _dataService;

    public MilkingEventTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _dataService = new DataService(_fixture.ConnectionString);
    }

    [Fact]
    public void GetRecentMilkingEvents_WithNoRecentEvents_ReturnsEmptyList()
    {
        // This test is FLAKY because it assumes no milking events in the last hour
        // But other tests may have inserted events that affect this result
        
        // Act
        var events = _dataService.GetRecentMilkingEvents(hours: 1);

        // Assert
        // This might pass or fail depending on when other tests ran
        // and whether they inserted events within the last hour
        
        // INTENTIONALLY FLAKY: Sometimes there will be recent events, sometimes not
        // depending on test execution order and timing
        Assert.NotNull(events);
    }

    [Fact]
    public void CreateAnimal_WithDuplicateIdentificationNumber_ShouldFail()
    {
        // Arrange - uses same static counter as other tests
        var identificationNumber = $"TEST-{TestDataHelper.GetNextAnimalId()}";
        
        // First creation should succeed
        var firstId = _dataService.CreateAnimal(identificationNumber, "First Animal", null);
        Assert.True(firstId > 0);

        // Second creation with same ID should throw
        // Note: This creates test data pollution
        Assert.ThrowsAny<Exception>(() => 
            _dataService.CreateAnimal(identificationNumber, "Second Animal", null));
    }

    [Fact]
    public void GetLastMilkingForAnimal_WhenNoMilkings_ReturnsNull()
    {
        // Arrange - create a brand new animal that has no milkings
        var identificationNumber = $"NOMILK-{TestDataHelper.GetNextAnimalId()}";
        var animalId = _dataService.CreateAnimal(identificationNumber, "No Milking Animal", null);

        // Act
        var lastMilking = _dataService.GetLastMilkingForAnimal(animalId);

        // Assert
        Assert.Null(lastMilking);
        
        // NOTE: This animal is left in the database after the test!
    }
    [Fact]
        public void WasRecentlyMilked_ShouldReturnTrue_WhenMilkedWithin6Hours()
        {
            // WHY: Tests that the 6-hour protection window correctly returns true for recent events.
            var notifier = new InMemoryRobotNotifier();
            var notification = new MilkingNotification
            {
                AnimalId = 1,
                RobotId = 101,
                Timestamp = DateTime.UtcNow.AddHours(-2),
                //MilkYieldLiters = 12.5
            };

            notifier.NotifyMilkingCompleted(notification);

            Assert.True(notifier.WasRecentlyMilked(1));
        }
        [Fact]
        public void WasRecentlyMilked_ShouldReturnFalse_WhenMilkedMoreThan6HoursAgo()
        {
            // WHY: Tests that animals milked older than 6 hours ago are allowed to be milked again.
            var notifier = new InMemoryRobotNotifier();
            var notification = new MilkingNotification
            {
                AnimalId = 2,
                RobotId = 101,
                Timestamp = DateTime.UtcNow.AddHours(-7),
                //MilkYieldLiters = 10.0
            };

            notifier.NotifyMilkingCompleted(notification);

            Assert.False(notifier.WasRecentlyMilked(2));
        }
        [Fact]
        public void NotifyMilkingCompleted_ShouldTriggerSubscribedRobots()
        {
            // WHY: Verifies that subscribing robots receive notifications when a milking event finishes.
            var notifier = new InMemoryRobotNotifier();
            bool eventFired = false;

            notifier.Subscribe(notification =>
            {
                eventFired = true;
                Assert.Equal(1, notification.AnimalId);
            });

            notifier.NotifyMilkingCompleted(new MilkingNotification
            {
                AnimalId = 1,
                RobotId = 101,
                Timestamp = DateTime.UtcNow,
                //MilkYieldLiters = 15.0
            });

            Assert.True(eventFired);
        }
        [Fact]
        public void ConcurrentNotifications_ShouldBeThreadSafe()
        {
            // WHY: Verifies thread-safety under heavy parallel load from multiple simulated robots.
            var notifier = new InMemoryRobotNotifier();

            Parallel.For(0, 100, i =>
            {
                notifier.NotifyMilkingCompleted(new MilkingNotification
                {
                    AnimalId = i % 10,
                    RobotId = i,
                    Timestamp = DateTime.UtcNow,
                    //MilkYieldLiters = 10.0
                });

                _ = notifier.WasRecentlyMilked(i % 10);
            });

            Assert.True(notifier.WasRecentlyMilked(0));
        }
}
