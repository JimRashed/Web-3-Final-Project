using System.Text.Json;
using FluentAssertions;
using QuizArena.Lobby.Api.Contracts;
using Xunit;

namespace QuizArena.Lobby.UnitTests.Services;

public sealed class LobbyContractTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PublicResponse_Serialized_ContainsOnlyApprovedSixFields()
    {
        // Arrange
        var response = new LobbyResponse(Guid.NewGuid(), "ABC123", "Open", 1, true, true);

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, WebOptions));

        // Assert
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "lobbyId", "joinCode", "status", "memberCount", "isMember", "isHost");
        json.RootElement.GetProperty("status").GetString().Should().Be("Open");
    }

    [Fact]
    public void ProvisionResponse_Serialized_ContainsOnlyApprovedFiveFields()
    {
        // Arrange
        var response = new LobbyProvisionResponse(Guid.NewGuid(), "ABC123", "Locked", "Host", ["Host", "guest"]);

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, WebOptions));

        // Assert
        json.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "lobbyId", "joinCode", "status", "hostUserId", "playerIds");
        json.RootElement.GetProperty("status").GetString().Should().Be("Locked");
        json.RootElement.GetProperty("playerIds").EnumerateArray().Select(value => value.GetString()).Should().Equal("Host", "guest");
    }

    [Fact]
    public void ProvisionResponse_SourceCollectionChanges_RetainsReadOnlySnapshot()
    {
        // Arrange
        var source = new List<string> { "Host" };
        var response = new LobbyProvisionResponse(Guid.NewGuid(), "ABC123", "Open", "Host", source);

        // Act
        source.Add("guest");

        // Assert
        response.PlayerIds.Should().Equal("Host");
        Action mutate = () => ((ICollection<string>)response.PlayerIds).Add("intruder");
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void EmptyRequest_EmptyObject_DeserializesWithoutProperties()
    {
        // Arrange
        // Act
        var request = JsonSerializer.Deserialize<EmptyLobbyRequest>("{}", WebOptions);

        // Assert
        request.Should().NotBeNull();
        JsonSerializer.Serialize(request, WebOptions).Should().Be("{}");
    }

    [Theory]
    [InlineData("{\"hostId\":\"forged\"}")]
    [InlineData("{\"HostId\":\"forged\"}")]
    [InlineData("{\"playerId\":\"forged\"}")]
    [InlineData("{\"PLAYERID\":\"forged\"}")]
    [InlineData("{\"unexpected\":1}")]
    [InlineData("[]")]
    [InlineData("\"string\"")]
    [InlineData("1")]
    [InlineData("true")]
    public void EmptyRequest_UnknownMembersOrWrongJsonType_IsRejected(string json)
    {
        // Arrange
        // Act
        Action act = () => JsonSerializer.Deserialize<EmptyLobbyRequest>(json, WebOptions);

        // Assert
        act.Should().Throw<JsonException>();
    }
}
