using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using QuizArena.Gameplay.Api.Services;
using QuizArena.Gameplay.Api.Services.Outbox;
using QuizArena.Gameplay.Domain.Repositories;
using Xunit;

namespace QuizArena.Gameplay.UnitTests.Outbox;

public sealed class DeliveryChannelsTests
{
    private const string Payload = "{\"matchId\":\"m-1\"}";

    private static HttpResponseMessage Respond(int status, TimeSpan? retryAfter = null)
    {
        var response = StubHttpMessageHandler.Json((HttpStatusCode)status);
        if (retryAfter is { } wait)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        }

        return response;
    }

    private static (StatisticsResultChannel Channel, StubHttpMessageHandler Handler, StubTokenSource Tokens, List<HttpResponseMessage> Responses)
        ResultChannel(params HttpResponseMessage[] responses)
    {
        var queue = new Queue<HttpResponseMessage>(responses);
        var handler = new StubHttpMessageHandler(_ => queue.Dequeue());
        var tokens = new StubTokenSource();
        var channel = new StatisticsResultChannel(
            new StubHttpClientFactory(handler),
            tokens,
            TestOptions.Services(),
            NullLogger<StatisticsResultChannel>.Instance);
        return (channel, handler, tokens, [.. responses]);
    }

    [Fact]
    public async Task SendAsync_WhenTheReceiverAccepts_MarksDelivered()
    {
        // Arrange
        var (channel, handler, tokens, _) = ResultChannel(Respond(200));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Delivered);
        tokens.Scopes.Should().Equal(GameplayServiceOptions.Scopes.ResultsWrite);
        handler.Requests.Single().Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests.Single().RequestUri!.ToString().Should().Be("http://statistics.test/api/results");
    }

    [Fact]
    public async Task SendAsync_WhenThrottled_IsTransientAndHonoursRetryAfter()
    {
        // Arrange
        var (channel, _, _, _) = ResultChannel(Respond(429, TimeSpan.FromSeconds(30)));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Transient);
        attempt.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(408)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SendAsync_WhenTheReceiverIsUnavailable_IsTransient(int status)
    {
        // Arrange
        var (channel, _, _, _) = ResultChannel(Respond(status));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Transient);
        attempt.Error.Should().Contain(status.ToString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task SendAsync_WhenThePayloadIsRejected_IsPermanent(int status)
    {
        // Arrange
        var (channel, _, _, _) = ResultChannel(Respond(status));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Permanent);
    }

    [Fact]
    public async Task SendAsync_WhenTheTokenExpired_RefreshesOnceAndSucceeds()
    {
        // Arrange
        var (channel, handler, tokens, _) = ResultChannel(Respond(401), Respond(200));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Delivered);
        handler.Requests.Should().HaveCount(2);
        tokens.Invalidated.Should().Equal(GameplayServiceOptions.Scopes.ResultsWrite);
    }

    [Fact]
    public async Task SendAsync_WhenTheFreshTokenIsAlsoRejected_IsPermanent()
    {
        // Arrange
        var (channel, handler, _, _) = ResultChannel(Respond(401), Respond(401));

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Permanent);
        attempt.Error.Should().Contain("token");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendAsync_WhenTheDestinationIsNotConfigured_IsTransientNotA500()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => Respond(200));
        var channel = new NotificationEventChannel(
            new StubHttpClientFactory(handler),
            new StubTokenSource(),
            TestOptions.Services(notification: string.Empty),
            NullLogger<NotificationEventChannel>.Instance);

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Transient);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenTheTransportFails_IsTransient()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("socket"));
        var channel = new StatisticsResultChannel(
            new StubHttpClientFactory(handler),
            new StubTokenSource(),
            TestOptions.Services(),
            NullLogger<StatisticsResultChannel>.Instance);

        // Act
        var attempt = await channel.SendAsync(Payload, CancellationToken.None);

        // Assert
        attempt.Disposition.Should().Be(DeliveryDisposition.Transient);
        attempt.Error.Should().Contain("transport");
    }

    [Fact]
    public void Channels_MapTheirDestinationsToTheFrozenOperations()
    {
        var statistics = new StatisticsResultChannel(
            new StubHttpClientFactory(new StubHttpMessageHandler(_ => Respond(200))),
            new StubTokenSource(),
            TestOptions.Services(),
            NullLogger<StatisticsResultChannel>.Instance);
        var notification = new NotificationEventChannel(
            new StubHttpClientFactory(new StubHttpMessageHandler(_ => Respond(200))),
            new StubTokenSource(),
            TestOptions.Services(),
            NullLogger<NotificationEventChannel>.Instance);

        statistics.Destination.Should().Be(MatchDelivery.Statistics);
        statistics.Operation.Should().Be(MatchDeliveries.ResultOperation);
        notification.Destination.Should().Be(MatchDelivery.Notification);
        notification.Operation.Should().Be(MatchDeliveries.EventOperation);
    }
}
