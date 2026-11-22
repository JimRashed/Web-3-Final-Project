using QuizArena.Identity.Domain.Exceptions;

namespace QuizArena.Identity.Domain.Entities;

/// <summary>
/// The single private picture owned by one <see cref="PlayerProfile"/> (keyed by the profile's id).
/// Kept outside the profile aggregate so profile reads never load image bytes and renames never contend with uploads.
/// </summary>
public sealed class ProfilePicture
{
    /// <summary>Hard upper bound for stored bytes (2 MiB, SEC-05). The API enforces the same limit on upload.</summary>
    public const int MaxContentLength = 2_097_152;

    public const int MaxContentTypeLength = 32;

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
    };

    private ProfilePicture(Guid profileId, string contentType, byte[] content, int version, DateTime updatedAt)
    {
        ProfileId = profileId;
        ContentType = contentType;
        Content = content;
        Version = version;
        UpdatedAt = updatedAt;
    }

    /// <summary>Exact, case-sensitive content types that may be stored. SVG and GIF are not allowed.</summary>
    public static IReadOnlySet<string> AllowedContentTypes => AllowedTypes;

    /// <summary>Id of the owning profile; also the picture's primary key.</summary>
    public Guid ProfileId { get; }

    public string ContentType { get; private set; }

    public byte[] Content { get; private set; }

    /// <summary>Starts at 1 and grows by exactly 1 on every successful replacement.</summary>
    public int Version { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Creates the first picture for a profile (version 1). The content is copied.</summary>
    public static ProfilePicture Create(Guid profileId, string contentType, byte[] content, DateTime updatedAt)
    {
        EnsureProfileId(profileId);
        ValidateContentType(contentType);
        ValidateContent(content);
        EnsureUtc(updatedAt);
        return new ProfilePicture(profileId, contentType, (byte[])content.Clone(), 1, updatedAt);
    }

    /// <summary>
    /// Rebuilds a stored picture from persisted values, re-checking every invariant. <c>ProfilePictureStore</c> maps its
    /// reads through this method (EF's own constructor binding does not call it), so a corrupt row throws
    /// <see cref="InvalidProfileException"/> here instead of reaching callers. The content is copied.
    /// </summary>
    public static ProfilePicture Rehydrate(Guid profileId, string contentType, byte[] content, int version, DateTime updatedAt)
    {
        EnsureProfileId(profileId);
        ValidateContent(content);
        _ = RehydrateInfo(contentType, version, updatedAt);
        return new ProfilePicture(profileId, contentType, (byte[])content.Clone(), version, updatedAt);
    }

    /// <summary>
    /// Validates the persisted reference values (content type, version, UTC timestamp) read without the bytes and
    /// returns them as a <see cref="ProfilePictureInfo"/>. Throws <see cref="InvalidProfileException"/> for a corrupt row.
    /// </summary>
    public static ProfilePictureInfo RehydrateInfo(string contentType, int version, DateTime updatedAt)
    {
        ValidateContentType(contentType);
        EnsureUtc(updatedAt);
        if (version < 1)
        {
            throw new InvalidProfileException(nameof(version), "Picture version must be at least 1.");
        }

        return new ProfilePictureInfo(contentType, version, updatedAt);
    }

    /// <summary>
    /// Replaces the bytes and content type and increments <see cref="Version"/> by one. All input is validated first,
    /// so a rejected replacement leaves the picture unchanged. The content is copied.
    /// </summary>
    public void Replace(string contentType, byte[] content, DateTime updatedAt)
    {
        ValidateContentType(contentType);
        ValidateContent(content);
        EnsureUtc(updatedAt);

        ContentType = contentType;
        Content = (byte[])content.Clone();
        Version = checked(Version + 1);
        UpdatedAt = updatedAt;
    }

    /// <summary>The approved reference for callers: no key, path, URL or bytes.</summary>
    public ProfilePictureInfo ToInfo() => new(ContentType, Version, UpdatedAt);

    private static void EnsureProfileId(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new InvalidProfileException(nameof(profileId), "Picture profile id must not be empty.");
        }
    }

    private static void ValidateContentType(string contentType)
    {
        if (contentType is null || !AllowedTypes.Contains(contentType))
        {
            throw new InvalidProfileException(nameof(contentType), "Picture content type must be image/png, image/jpeg or image/webp.");
        }
    }

    private static void ValidateContent(byte[] content)
    {
        if (content is null || content.Length == 0)
        {
            throw new InvalidProfileException(nameof(content), "Picture content is required.");
        }

        if (content.Length > MaxContentLength)
        {
            throw new InvalidProfileException(nameof(content), $"Picture content must be at most {MaxContentLength} bytes.");
        }
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc || value == default)
        {
            throw new InvalidProfileException("updatedAt", "Picture updatedAt must be a UTC timestamp.");
        }
    }
}
