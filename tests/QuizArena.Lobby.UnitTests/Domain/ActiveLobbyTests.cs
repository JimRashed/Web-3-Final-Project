using FluentAssertions;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Enums;
using QuizArena.Lobby.Domain.Exceptions;
using Xunit;

namespace QuizArena.Lobby.UnitTests.Domain;

public sealed class ActiveLobbyTests
{
    [Fact]
    public void Create_ValidInput_StartsOpenWithHostExactlyOnce()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string host = "issuer|Host";

        // Act
        var lobby = ActiveLobby.Create(host, "AB12Z9", id);

        // Assert
        lobby.Id.Should().Be(id);
        lobby.HostId.Should().Be(host);
        lobby.JoinCode.Should().Be("AB12Z9");
        lobby.Status.Should().Be(LobbyStatus.Open);
        lobby.Players.Should().ContainSingle().Which.PlayerId.Should().Be(host);
        lobby.Players[0].LobbyId.Should().Be(id);
        lobby.Players[0].Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData(" host")]
    [InlineData("host ")]
    public void Create_InvalidHost_ThrowsTypedInvalidInput(string? host)
    {
        // Arrange
        // Act
        Action act = () => ActiveLobby.Create(host!, "ABC123");

        // Assert
        act.Should().Throw<InvalidLobbyException>().Which.Field.Should().Be("hostId");
    }

    [Fact]
    public void Create_HostOverMaximumLength_ThrowsTypedInvalidInput()
    {
        // Arrange
        var host = new string('a', 201);

        // Act
        Action act = () => ActiveLobby.Create(host, "ABC123");

        // Assert
        act.Should().Throw<InvalidLobbyException>();
    }

    [Fact]
    public void Create_HostAtMaximumLength_PreservesExactSubject()
    {
        // Arrange
        var host = new string('a', 200);

        // Act
        var lobby = ActiveLobby.Create(host, "ABC123");

        // Assert
        lobby.HostId.Should().Be(host);
    }

    [Fact]
    public void Create_EmptyLobbyId_ThrowsTypedInvalidInput()
    {
        // Arrange
        // Act
        Action act = () => ActiveLobby.Create("host", "ABC123", Guid.Empty);

        // Assert
        act.Should().Throw<InvalidLobbyException>();
    }

    [Fact]
    public void Join_SecondDistinctSubject_AddsMembership()
    {
        // Arrange
        var lobby = ActiveLobby.Create("host", "ABC123");

        // Act
        var changed = lobby.Join("issuer|guest");

        // Assert
        changed.Should().BeTrue();
        lobby.Players.Select(player => player.PlayerId).Should().Equal("host", "issuer|guest");
        lobby.Players.Select(player => player.Id).Should().OnlyHaveUniqueItems();
        lobby.Players.Should().OnlyContain(player => player.LobbyId == lobby.Id);
        lobby.Status.Should().Be(LobbyStatus.Open);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("guest")]
    public void Join_ExistingOpenMember_IsUnchangedEvenAtCapacity(string subject)
    {
        // Arrange
        var lobby = TwoPlayerLobby();
        var members = lobby.Players.ToArray();

        // Act
        var changed = lobby.Join(subject);

        // Assert
        changed.Should().BeFalse();
        lobby.Players.Should().Equal(members);
        lobby.Status.Should().Be(LobbyStatus.Open);
    }

    [Fact]
    public void Join_SubjectDiffersOnlyByCase_TreatsItAsDistinct()
    {
        // Arrange
        var lobby = ActiveLobby.Create("host", "ABC123");

        // Act
        var changed = lobby.Join("HOST");

        // Assert
        changed.Should().BeTrue();
        lobby.Players.Select(player => player.PlayerId).Should().Equal("host", "HOST");
    }

    [Fact]
    public void Join_ThirdDistinctSubject_ConflictsWithoutMutation()
    {
        // Arrange
        var lobby = TwoPlayerLobby();
        var members = lobby.Players.ToArray();

        // Act
        Action act = () => lobby.Join("third");

        // Assert
        act.Should().Throw<LobbyConflictException>();
        lobby.Players.Should().Equal(members);
        lobby.Status.Should().Be(LobbyStatus.Open);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("guest")]
    [InlineData("third")]
    public void Join_LockedLobby_ConflictsForExistingAndNewMembers(string subject)
    {
        // Arrange
        var lobby = TwoPlayerLobby();
        lobby.Lock("host");
        var members = lobby.Players.ToArray();

        // Act
        Action act = () => lobby.Join(subject);

        // Assert
        act.Should().Throw<LobbyConflictException>();
        lobby.Players.Should().Equal(members);
        lobby.Status.Should().Be(LobbyStatus.Locked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" guest")]
    [InlineData("guest\n")]
    public void Join_InvalidSubject_RejectsWithoutMutation(string? subject)
    {
        // Arrange
        var lobby = ActiveLobby.Create("host", "ABC123");

        // Act
        Action act = () => lobby.Join(subject!);

        // Assert
        act.Should().Throw<InvalidLobbyException>();
        lobby.Players.Should().ContainSingle();
    }

    [Fact]
    public void Lock_HostWithTwoMembers_TransitionsToLocked()
    {
        // Arrange
        var lobby = TwoPlayerLobby();

        // Act
        var changed = lobby.Lock("host");

        // Assert
        changed.Should().BeTrue();
        lobby.Status.Should().Be(LobbyStatus.Locked);
        lobby.Players.Should().HaveCount(2);
    }

    [Fact]
    public void Lock_HostAlone_ConflictsWithoutMutation()
    {
        // Arrange
        var lobby = ActiveLobby.Create("host", "ABC123");

        // Act
        Action act = () => lobby.Lock("host");

        // Assert
        act.Should().Throw<LobbyConflictException>();
        lobby.Status.Should().Be(LobbyStatus.Open);
        lobby.Players.Should().ContainSingle();
    }

    [Theory]
    [InlineData("guest", false)]
    [InlineData("HOST", false)]
    [InlineData("guest", true)]
    [InlineData("HOST", true)]
    public void Lock_Nonhost_IsForbiddenBeforeStateHandling(string subject, bool locked)
    {
        // Arrange
        var lobby = TwoPlayerLobby();
        if (locked)
        {
            lobby.Lock("host");
        }

        var status = lobby.Status;

        // Act
        Action act = () => lobby.Lock(subject);

        // Assert
        act.Should().Throw<LobbyForbiddenException>();
        lobby.Status.Should().Be(status);
    }

    [Fact]
    public void Lock_HostAlreadyLocked_IsUnchanged()
    {
        // Arrange
        var lobby = TwoPlayerLobby();
        lobby.Lock("host");
        var members = lobby.Players.ToArray();

        // Act
        var changed = lobby.Lock("host");

        // Assert
        changed.Should().BeFalse();
        lobby.Status.Should().Be(LobbyStatus.Locked);
        lobby.Players.Should().Equal(members);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("host ")]
    public void Lock_InvalidSubject_ThrowsTypedInvalidInput(string? subject)
    {
        // Arrange
        var lobby = TwoPlayerLobby();

        // Act
        Action act = () => lobby.Lock(subject!);

        // Assert
        act.Should().Throw<InvalidLobbyException>();
        lobby.Status.Should().Be(LobbyStatus.Open);
    }

    [Fact]
    public void Players_CollectionMutationAttempt_IsRejected()
    {
        // Arrange
        var lobby = ActiveLobby.Create("host", "ABC123");
        var exposedCollection = (ICollection<LobbyPlayer>)lobby.Players;

        // Act
        Action act = () => exposedCollection.Add(LobbyPlayer.Create(lobby.Id, "guest"));

        // Assert
        act.Should().Throw<NotSupportedException>();
        lobby.Players.Should().ContainSingle();
    }

    [Theory]
    [InlineData(LobbyStatus.Open, 1)]
    [InlineData(LobbyStatus.Open, 2)]
    [InlineData(LobbyStatus.Locked, 2)]
    public void Rehydrate_ValidStoredState_PreservesValuesAndCopiesRoster(LobbyStatus status, int count)
    {
        // Arrange
        var id = Guid.NewGuid();
        var players = new List<LobbyPlayer> { LobbyPlayer.Create(id, "host") };
        if (count == 2)
        {
            players.Add(LobbyPlayer.Create(id, "guest"));
        }

        var expectedPlayers = players.ToArray();

        // Act
        var lobby = ActiveLobby.Rehydrate(id, "ABC123", "host", status, players);
        players.Clear();

        // Assert
        lobby.Id.Should().Be(id);
        lobby.HostId.Should().Be("host");
        lobby.JoinCode.Should().Be("ABC123");
        lobby.Status.Should().Be(status);
        lobby.Players.Should().Equal(expectedPlayers);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null-roster")]
    [InlineData("null-member")]
    [InlineData("too-many")]
    [InlineData("missing-host")]
    [InlineData("duplicate-subject")]
    [InlineData("duplicate-id")]
    [InlineData("wrong-lobby")]
    [InlineData("locked-alone")]
    [InlineData("undefined-state")]
    public void Rehydrate_CorruptStoredState_ThrowsTypedInvalidInput(string corruption)
    {
        // Arrange
        var id = Guid.NewGuid();
        var host = LobbyPlayer.Create(id, "host");
        var players = new List<LobbyPlayer> { host };
        var status = LobbyStatus.Open;
        switch (corruption)
        {
            case "empty": players.Clear(); break;
            case "null-roster": players = null!; break;
            case "null-member": players.Add(null!); break;
            case "too-many": players.AddRange([LobbyPlayer.Create(id, "guest"), LobbyPlayer.Create(id, "third")]); break;
            case "missing-host": players = [LobbyPlayer.Create(id, "guest")]; break;
            case "duplicate-subject": players.Add(LobbyPlayer.Create(id, "host")); break;
            case "duplicate-id": players.Add(LobbyPlayer.Create(id, "guest", host.Id)); break;
            case "wrong-lobby": players.Add(LobbyPlayer.Create(Guid.NewGuid(), "guest")); break;
            case "locked-alone": status = LobbyStatus.Locked; break;
            case "undefined-state": status = (LobbyStatus)42; break;
        }

        // Act
        Action act = () => ActiveLobby.Rehydrate(id, "ABC123", "host", status, players);

        // Assert
        act.Should().Throw<InvalidLobbyException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rehydrate_EmptyLobbyOrMembershipId_ThrowsTypedInvalidInput(bool emptyLobby)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        Action act = () => LobbyPlayer.Rehydrate(emptyLobby ? id : Guid.Empty, emptyLobby ? Guid.Empty : id, "host");

        // Assert
        act.Should().Throw<InvalidLobbyException>();
    }

    [Fact]
    public void Rehydrate_EmptyAggregateId_ThrowsTypedInvalidInput()
    {
        // Arrange
        var players = new[] { LobbyPlayer.Create(Guid.NewGuid(), "host") };

        // Act
        Action act = () => ActiveLobby.Rehydrate(Guid.Empty, "ABC123", "host", LobbyStatus.Open, players);

        // Assert
        act.Should().Throw<InvalidLobbyException>();
    }

    private static ActiveLobby TwoPlayerLobby()
    {
        var lobby = ActiveLobby.Create("host", "ABC123");
        lobby.Join("guest");
        return lobby;
    }
}
