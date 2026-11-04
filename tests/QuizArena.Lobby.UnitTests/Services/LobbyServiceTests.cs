using System.Security.Claims;
using FluentAssertions;
using Moq;
using QuizArena.Lobby.Api.Contracts;
using QuizArena.Lobby.Api.Security;
using QuizArena.Lobby.Api.Services;
using QuizArena.Lobby.Data.Repositories;
using QuizArena.Lobby.Domain.Entities;
using QuizArena.Lobby.Domain.Exceptions;
using Xunit;

namespace QuizArena.Lobby.UnitTests.Services;

public sealed class LobbyServiceTests
{
    private const string Code = "ABC123";

    [Fact]
    public async Task Create_AuthenticatedUser_UsesOnlyTrustedExactSubject()
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        var lobby = ActiveLobby.Create("issuer|Host", Code);
        repository.Setup(value => value.CreateAsync("issuer|Host", CancellationToken.None)).ReturnsAsync(lobby);
        var service = new LobbyService(repository.Object);

        // Act
        var response = await service.CreateAsync(Actor("issuer|Host"));

        // Assert
        response.Should().Be(new LobbyResponse(lobby.Id, Code, "Open", 1, true, true));
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Host", true, true)]
    [InlineData("guest", true, false)]
    [InlineData("outsider", false, false)]
    [InlineData("HOST", false, false)]
    public async Task Preview_AuthenticatedViewer_MapsOrdinalCallerRelativeFlags(string subject, bool isMember, bool isHost)
    {
        // Arrange
        var lobby = FullLobby();
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByJoinCodeAsync(Code, CancellationToken.None)).ReturnsAsync(lobby);

        // Act
        var response = await new LobbyService(repository.Object).PreviewAsync(Actor(subject), Code);

        // Assert
        response!.IsMember.Should().Be(isMember);
        response.IsHost.Should().Be(isHost);
        response.MemberCount.Should().Be(2);
        response.Status.Should().Be("Open");
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Join_TrustedSubject_MapsActualReturnedAggregate()
    {
        // Arrange
        var lobby = ActiveLobby.Create("Host", Code);
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        repository.Setup(value => value.JoinAsync(Code, "HOST", CancellationToken.None))
            .Returns((string _, string subject, CancellationToken _) =>
            {
                lobby.Join(subject);
                return Task.FromResult<ActiveLobby?>(lobby);
            });

        // Act
        var response = await new LobbyService(repository.Object).JoinAsync(Actor("HOST"), Code);

        // Assert
        response!.IsHost.Should().BeFalse();
        response.IsMember.Should().BeTrue();
        response.MemberCount.Should().Be(2);
        lobby.Players.Select(player => player.PlayerId).Should().Equal("Host", "HOST");
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("guest", false, false)]
    [InlineData("third", false, true)]
    [InlineData("guest", true, true)]
    public async Task Join_RepositoryExecutesDomain_PreservesDuplicateFullAndLockedOutcomes(string subject, bool locked, bool conflicts)
    {
        // Arrange
        var lobby = FullLobby();
        if (locked)
        {
            lobby.Lock("Host");
        }

        var ids = lobby.Players.Select(player => player.Id).ToArray();
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        repository.Setup(value => value.JoinAsync(Code, subject, CancellationToken.None))
            .Returns((string _, string player, CancellationToken _) =>
            {
                lobby.Join(player);
                return Task.FromResult<ActiveLobby?>(lobby);
            });

        // Act
        Func<Task> act = () => new LobbyService(repository.Object).JoinAsync(Actor(subject), Code);

        // Assert
        if (conflicts)
        {
            await act.Should().ThrowAsync<LobbyConflictException>();
        }
        else
        {
            await act.Should().NotThrowAsync();
        }

        lobby.Players.Select(player => player.Id).Should().Equal(ids);
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task Workflow_MissingLobby_ReturnsNull(string operation)
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        SetupResult(repository, operation, null, CancellationToken.None);

        // Act
        var result = await InvokeAsync(new LobbyService(repository.Object), operation, PrincipalFor(operation));

        // Assert
        result.Should().BeNull();
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> InvalidActors() =>
        from operation in new[] { "create", "preview", "join", "lock", "verify" }
        from scenario in new[] { "null", "anonymous", "unauthenticated", "missing-subject", "blank-issuer", "missing-actor", "unknown-actor" }
        select new object[] { operation, scenario };

    [Theory]
    [MemberData(nameof(InvalidActors))]
    public async Task Workflow_InvalidPrincipal_RejectsBeforePersistence(string operation, string scenario)
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        var principal = InvalidActor(scenario);

        // Act
        Func<Task> act = () => InvokeAsync(new LobbyService(repository.Object), operation, principal!);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("An authenticated QuizArena actor is required.");
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task Workflow_WrongActorKind_IsForbiddenWithoutPersistence(string operation)
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        var principal = Actor("Host", operation == "verify" ? QuizArenaClaims.UserActor : QuizArenaClaims.MachineActor);

        // Act
        Func<Task> act = () => InvokeAsync(new LobbyService(repository.Object), operation, principal);

        // Assert
        await act.Should().ThrowAsync<LobbyForbiddenException>();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task Workflow_InvalidCode_RejectsWithoutNormalizationOrPersistence(string operation)
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);

        // Act
        Func<Task> act = () => InvokeAsync(new LobbyService(repository.Object), operation, PrincipalFor(operation), code: "abc123");

        // Assert
        await act.Should().ThrowAsync<InvalidLobbyException>();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task Workflow_ValidRequest_ForwardsExactCancellationToken(string operation)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        SetupResult(repository, operation, FullLobby(), cancellation.Token);

        // Act
        var result = await InvokeAsync(new LobbyService(repository.Object), operation, PrincipalFor(operation), cancellation.Token);

        // Assert
        result.Should().NotBeNull();
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("preview")]
    [InlineData("join")]
    [InlineData("lock")]
    [InlineData("verify")]
    public async Task Workflow_PreCancelled_DoesNoRepositoryWork(string operation)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);

        // Act
        Func<Task> act = () => InvokeAsync(new LobbyService(repository.Object), operation, PrincipalFor(operation), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        repository.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> DependencyFailures() =>
        from operation in new[] { "create", "preview", "join", "lock", "verify" }
        from failure in new[] { "domain", "persistence", "dependency", "cancellation" }
        select new object[] { operation, failure };

    [Theory]
    [MemberData(nameof(DependencyFailures))]
    public async Task Workflow_RepositoryFailure_PropagatesOriginalException(string operation, string failure)
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        Exception error = failure switch
        {
            "domain" => new LobbyConflictException("conflict"),
            "persistence" => new LobbyPersistenceException("safe", new InvalidOperationException()),
            "cancellation" => new OperationCanceledException(),
            _ => new InvalidOperationException("dependency"),
        };
        SetupFailure(repository, operation, error);

        // Act
        Func<Task> act = () => InvokeAsync(new LobbyService(repository.Object), operation, PrincipalFor(operation));

        // Assert
        var thrown = await act.Should().ThrowAsync<Exception>();
        thrown.Which.Should().BeSameAs(error);
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Lock_RetryableCallback_RechecksEachIndependentAggregateAndMapsPersistedResult()
    {
        // Arrange
        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        var first = FullLobby();
        var reloaded = FullLobby();
        var changes = new List<bool>();
        repository.Setup(value => value.SaveAsync(Code, It.IsAny<Func<ActiveLobby, bool>>(), CancellationToken.None))
            .Returns((string _, Func<ActiveLobby, bool> mutation, CancellationToken _) =>
            {
                changes.Add(mutation(first));
                changes.Add(mutation(reloaded));
                changes.Add(mutation(reloaded));
                return Task.FromResult<ActiveLobby?>(reloaded);
            });

        // Act
        var response = await new LobbyService(repository.Object).LockAsync(Actor(), Code);

        // Assert
        changes.Should().Equal(true, true, false);
        response!.LobbyId.Should().Be(reloaded.Id);
        response.Status.Should().Be("Locked");
        response.IsHost.Should().BeTrue();
        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("guest", false)]
    [InlineData("guest", true)]
    [InlineData("Host", false)]
    public async Task Lock_DomainRejectsCallback_PropagatesHostAndCapacityRules(string subject, bool locked)
    {
        // Arrange
        var lobby = subject == "Host" ? ActiveLobby.Create("Host", Code) : FullLobby();
        if (locked)
        {
            lobby.Lock("Host");
        }

        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        repository.Setup(value => value.SaveAsync(Code, It.IsAny<Func<ActiveLobby, bool>>(), CancellationToken.None))
            .Returns((string _, Func<ActiveLobby, bool> mutation, CancellationToken _) =>
            {
                mutation(lobby);
                return Task.FromResult<ActiveLobby?>(lobby);
            });

        // Act
        Func<Task> act = () => new LobbyService(repository.Object).LockAsync(Actor(subject), Code);

        // Assert
        if (subject == "Host")
        {
            await act.Should().ThrowAsync<LobbyConflictException>();
        }
        else
        {
            await act.Should().ThrowAsync<LobbyForbiddenException>();
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Verify_ValidMachine_ReturnsCurrentStateAndCopiedSubjects(int members, bool locked)
    {
        // Arrange
        var lobby = members == 1 ? ActiveLobby.Create("Host", Code) : FullLobby();
        if (locked)
        {
            lobby.Lock("Host");
        }

        var repository = new Mock<ILobbyRepository>(MockBehavior.Strict);
        repository.Setup(value => value.GetByJoinCodeAsync(Code, CancellationToken.None)).ReturnsAsync(lobby);

        // Act
        var response = await new LobbyService(repository.Object).VerifyAsync(Actor("machine", QuizArenaClaims.MachineActor), Code);

        // Assert
        response!.LobbyId.Should().Be(lobby.Id);
        response.HostUserId.Should().Be("Host");
        response.Status.Should().Be(locked ? "Locked" : "Open");
        response.PlayerIds.Should().Equal(lobby.Players.Select(player => player.PlayerId));
        if (members == 1)
        {
            lobby.Join("later");
            response.PlayerIds.Should().Equal("Host");
        }

        repository.VerifyAll();
        repository.VerifyNoOtherCalls();
    }

    private static ClaimsPrincipal Actor(string subject = "Host", string kind = QuizArenaClaims.UserActor, bool authenticated = true) => new(
        new ClaimsIdentity([
            new Claim(QuizArenaClaims.Subject, subject),
            new Claim(QuizArenaClaims.Issuer, "https://issuer.example"),
            new Claim(QuizArenaClaims.Actor, kind),
        ], authenticated ? "application-test" : null));

    private static ClaimsPrincipal PrincipalFor(string operation) => Actor(kind: operation == "verify" ? QuizArenaClaims.MachineActor : QuizArenaClaims.UserActor);

    private static ClaimsPrincipal? InvalidActor(string scenario)
    {
        if (scenario == "null")
        {
            return null;
        }

        if (scenario == "anonymous")
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var identity = (ClaimsIdentity)Actor(authenticated: scenario != "unauthenticated").Identity!;
        var type = scenario switch
        {
            "missing-subject" => QuizArenaClaims.Subject,
            "blank-issuer" => QuizArenaClaims.Issuer,
            _ => QuizArenaClaims.Actor,
        };
        if (scenario != "unauthenticated")
        {
            identity.RemoveClaim(identity.FindFirst(type)!);
        }

        if (scenario == "blank-issuer")
        {
            identity.AddClaim(new Claim(QuizArenaClaims.Issuer, " "));
        }

        if (scenario == "unknown-actor")
        {
            identity.AddClaim(new Claim(QuizArenaClaims.Actor, "robot"));
        }

        return new ClaimsPrincipal(identity);
    }

    private static ActiveLobby FullLobby()
    {
        var lobby = ActiveLobby.Create("Host", Code);
        lobby.Join("guest");
        return lobby;
    }

    private static async Task<object?> InvokeAsync(LobbyService service, string operation, ClaimsPrincipal principal, CancellationToken cancellationToken = default, string code = Code) => operation switch
    {
        "create" => await service.CreateAsync(principal, cancellationToken),
        "preview" => await service.PreviewAsync(principal, code, cancellationToken),
        "join" => await service.JoinAsync(principal, code, cancellationToken),
        "lock" => await service.LockAsync(principal, code, cancellationToken),
        _ => await service.VerifyAsync(principal, code, cancellationToken),
    };

    private static void SetupResult(Mock<ILobbyRepository> repository, string operation, ActiveLobby? lobby, CancellationToken token)
    {
        switch (operation)
        {
            case "create": repository.Setup(value => value.CreateAsync("Host", token)).ReturnsAsync(lobby!); break;
            case "join": repository.Setup(value => value.JoinAsync(Code, "Host", token)).ReturnsAsync(lobby); break;
            case "lock": repository.Setup(value => value.SaveAsync(Code, It.IsAny<Func<ActiveLobby, bool>>(), token)).ReturnsAsync(lobby); break;
            default: repository.Setup(value => value.GetByJoinCodeAsync(Code, token)).ReturnsAsync(lobby); break;
        }
    }

    private static void SetupFailure(Mock<ILobbyRepository> repository, string operation, Exception error)
    {
        switch (operation)
        {
            case "create": repository.Setup(value => value.CreateAsync("Host", CancellationToken.None)).ThrowsAsync(error); break;
            case "join": repository.Setup(value => value.JoinAsync(Code, "Host", CancellationToken.None)).ThrowsAsync(error); break;
            case "lock": repository.Setup(value => value.SaveAsync(Code, It.IsAny<Func<ActiveLobby, bool>>(), CancellationToken.None)).ThrowsAsync(error); break;
            default: repository.Setup(value => value.GetByJoinCodeAsync(Code, CancellationToken.None)).ThrowsAsync(error); break;
        }
    }
}
