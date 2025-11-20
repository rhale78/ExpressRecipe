using ExpressRecipe.Data.Common;
using ExpressRecipe.Shared.DTOs.User;
using Microsoft.Data.SqlClient;

namespace ExpressRecipe.UserService.Data;

public interface IFriendsRepository
{
    // Friend Management
    Task<List<UserFriendDto>> GetUserFriendsAsync(Guid userId, string? status = null);
    Task<UserFriendDto?> GetFriendshipAsync(Guid userId, Guid friendUserId);
    Task<Guid> SendFriendRequestAsync(Guid userId, Guid friendUserId, string? notes = null);
    Task<bool> AcceptFriendRequestAsync(Guid friendRequestId, Guid acceptingUserId);
    Task<bool> RejectFriendRequestAsync(Guid friendRequestId, Guid rejectingUserId);
    Task<bool> BlockUserAsync(Guid userId, Guid userToBlockId, string? reason = null);
    Task<bool> UnblockUserAsync(Guid userId, Guid userToUnblockId);
    Task<bool> RemoveFriendAsync(Guid userId, Guid friendUserId);
    Task<FriendsSummaryDto> GetFriendsSummaryAsync(Guid userId);

    // Friend Invitations
    Task<Guid> SendInvitationAsync(Guid inviterId, string inviteeEmail, string? inviteePhone = null, string? message = null);
    Task<FriendInvitationDto?> GetInvitationByCodeAsync(string invitationCode);
    Task<bool> AcceptInvitationAsync(string invitationCode, Guid acceptingUserId);
    Task<List<FriendInvitationDto>> GetUserInvitationsAsync(Guid userId);
}

public class FriendsRepository : SqlHelper, IFriendsRepository
{
    public FriendsRepository(string connectionString) : base(connectionString)
    {
    }

    // Friend Management

    public async Task<List<UserFriendDto>> GetUserFriendsAsync(Guid userId, string? status = null)
    {
        var sql = @"
            SELECT uf.Id, uf.UserId, uf.FriendUserId, uf.Status, uf.RequestedBy,
                   uf.RequestedAt, uf.AcceptedAt, uf.BlockedAt, uf.BlockedBy, uf.Notes,
                   u.Email AS FriendUserName, up.DisplayName AS FriendDisplayName
            FROM UserFriend uf
            INNER JOIN [User] u ON uf.FriendUserId = u.Id
            LEFT JOIN UserProfile up ON uf.FriendUserId = up.UserId
            WHERE uf.UserId = @UserId";

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@UserId", userId)
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " AND uf.Status = @Status";
            parameters.Add(new SqlParameter("@Status", status));
        }

        sql += " ORDER BY uf.RequestedAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new UserFriendDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                FriendUserId = GetGuid(reader, "FriendUserId"),
                FriendUserName = GetString(reader, "FriendUserName"),
                FriendDisplayName = GetString(reader, "FriendDisplayName"),
                Status = GetString(reader, "Status") ?? string.Empty,
                RequestedBy = GetGuid(reader, "RequestedBy"),
                RequestedAt = GetDateTime(reader, "RequestedAt") ?? DateTime.UtcNow,
                AcceptedAt = GetDateTime(reader, "AcceptedAt"),
                BlockedAt = GetDateTime(reader, "BlockedAt"),
                BlockedBy = GetGuidNullable(reader, "BlockedBy"),
                Notes = GetString(reader, "Notes")
            },
            parameters.ToArray());
    }

    public async Task<UserFriendDto?> GetFriendshipAsync(Guid userId, Guid friendUserId)
    {
        const string sql = @"
            SELECT uf.Id, uf.UserId, uf.FriendUserId, uf.Status, uf.RequestedBy,
                   uf.RequestedAt, uf.AcceptedAt, uf.BlockedAt, uf.BlockedBy, uf.Notes,
                   u.Email AS FriendUserName, up.DisplayName AS FriendDisplayName
            FROM UserFriend uf
            INNER JOIN [User] u ON uf.FriendUserId = u.Id
            LEFT JOIN UserProfile up ON uf.FriendUserId = up.UserId
            WHERE (uf.UserId = @UserId AND uf.FriendUserId = @FriendUserId)
               OR (uf.UserId = @FriendUserId AND uf.FriendUserId = @UserId)";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new UserFriendDto
            {
                Id = GetGuid(reader, "Id"),
                UserId = GetGuid(reader, "UserId"),
                FriendUserId = GetGuid(reader, "FriendUserId"),
                FriendUserName = GetString(reader, "FriendUserName"),
                FriendDisplayName = GetString(reader, "FriendDisplayName"),
                Status = GetString(reader, "Status") ?? string.Empty,
                RequestedBy = GetGuid(reader, "RequestedBy"),
                RequestedAt = GetDateTime(reader, "RequestedAt") ?? DateTime.UtcNow,
                AcceptedAt = GetDateTime(reader, "AcceptedAt"),
                BlockedAt = GetDateTime(reader, "BlockedAt"),
                BlockedBy = GetGuidNullable(reader, "BlockedBy"),
                Notes = GetString(reader, "Notes")
            },
            new SqlParameter("@UserId", userId),
            new SqlParameter("@FriendUserId", friendUserId));

        return results.FirstOrDefault();
    }

    public async Task<Guid> SendFriendRequestAsync(Guid userId, Guid friendUserId, string? notes = null)
    {
        // Check if friendship already exists
        var existing = await GetFriendshipAsync(userId, friendUserId);
        if (existing != null)
        {
            throw new InvalidOperationException("Friend request already exists");
        }

        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserFriend (Id, UserId, FriendUserId, Status, RequestedBy, RequestedAt, Notes)
            VALUES (@Id, @UserId, @FriendUserId, 'Pending', @RequestedBy, GETUTCDATE(), @Notes)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@FriendUserId", friendUserId),
            new SqlParameter("@RequestedBy", userId),
            new SqlParameter("@Notes", (object?)notes ?? DBNull.Value));

        return id;
    }

    public async Task<bool> AcceptFriendRequestAsync(Guid friendRequestId, Guid acceptingUserId)
    {
        const string sql = @"
            UPDATE UserFriend
            SET Status = 'Accepted',
                AcceptedAt = GETUTCDATE()
            WHERE Id = @Id
              AND FriendUserId = @AcceptingUserId
              AND Status = 'Pending'";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", friendRequestId),
            new SqlParameter("@AcceptingUserId", acceptingUserId));

        // Create reciprocal friendship
        if (rowsAffected > 0)
        {
            const string getFriendship = @"
                SELECT UserId, FriendUserId, Notes
                FROM UserFriend
                WHERE Id = @Id";

            var friendship = await ExecuteReaderAsync(
                getFriendship,
                reader => new
                {
                    UserId = GetGuid(reader, "UserId"),
                    FriendUserId = GetGuid(reader, "FriendUserId"),
                    Notes = GetString(reader, "Notes")
                },
                new SqlParameter("@Id", friendRequestId));

            var original = friendship.FirstOrDefault();
            if (original != null)
            {
                const string reciprocalSql = @"
                    INSERT INTO UserFriend (Id, UserId, FriendUserId, Status, RequestedBy, RequestedAt, AcceptedAt, Notes)
                    VALUES (@Id, @UserId, @FriendUserId, 'Accepted', @RequestedBy, GETUTCDATE(), GETUTCDATE(), @Notes)";

                await ExecuteNonQueryAsync(reciprocalSql,
                    new SqlParameter("@Id", Guid.NewGuid()),
                    new SqlParameter("@UserId", original.FriendUserId),
                    new SqlParameter("@FriendUserId", original.UserId),
                    new SqlParameter("@RequestedBy", original.UserId),
                    new SqlParameter("@Notes", (object?)original.Notes ?? DBNull.Value));
            }
        }

        return rowsAffected > 0;
    }

    public async Task<bool> RejectFriendRequestAsync(Guid friendRequestId, Guid rejectingUserId)
    {
        const string sql = @"
            DELETE FROM UserFriend
            WHERE Id = @Id
              AND FriendUserId = @RejectingUserId
              AND Status = 'Pending'";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", friendRequestId),
            new SqlParameter("@RejectingUserId", rejectingUserId));

        return rowsAffected > 0;
    }

    public async Task<bool> BlockUserAsync(Guid userId, Guid userToBlockId, string? reason = null)
    {
        // Remove existing friendship if it exists
        await RemoveFriendAsync(userId, userToBlockId);

        var id = Guid.NewGuid();

        const string sql = @"
            INSERT INTO UserFriend (Id, UserId, FriendUserId, Status, RequestedBy, RequestedAt, BlockedAt, BlockedBy, Notes)
            VALUES (@Id, @UserId, @UserToBlockId, 'Blocked', @UserId, GETUTCDATE(), GETUTCDATE(), @UserId, @Reason)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@UserId", userId),
            new SqlParameter("@UserToBlockId", userToBlockId),
            new SqlParameter("@Reason", (object?)reason ?? DBNull.Value));

        return true;
    }

    public async Task<bool> UnblockUserAsync(Guid userId, Guid userToUnblockId)
    {
        const string sql = @"
            DELETE FROM UserFriend
            WHERE UserId = @UserId
              AND FriendUserId = @UserToUnblockId
              AND Status = 'Blocked'";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@UserToUnblockId", userToUnblockId));

        return rowsAffected > 0;
    }

    public async Task<bool> RemoveFriendAsync(Guid userId, Guid friendUserId)
    {
        const string sql = @"
            DELETE FROM UserFriend
            WHERE (UserId = @UserId AND FriendUserId = @FriendUserId)
               OR (UserId = @FriendUserId AND FriendUserId = @UserId)";

        var rowsAffected = await ExecuteNonQueryAsync(sql,
            new SqlParameter("@UserId", userId),
            new SqlParameter("@FriendUserId", friendUserId));

        return rowsAffected > 0;
    }

    public async Task<FriendsSummaryDto> GetFriendsSummaryAsync(Guid userId)
    {
        const string countsSql = @"
            SELECT
                COUNT(CASE WHEN Status = 'Accepted' THEN 1 END) AS TotalFriends,
                COUNT(CASE WHEN Status = 'Pending' AND FriendUserId = @UserId THEN 1 END) AS PendingRequests,
                COUNT(CASE WHEN Status = 'Pending' AND UserId = @UserId THEN 1 END) AS SentRequests,
                COUNT(CASE WHEN Status = 'Blocked' THEN 1 END) AS BlockedUsers
            FROM UserFriend
            WHERE UserId = @UserId OR (FriendUserId = @UserId AND Status = 'Pending')";

        var counts = await ExecuteReaderAsync(
            countsSql,
            reader => new
            {
                TotalFriends = GetInt(reader, "TotalFriends"),
                PendingRequests = GetInt(reader, "PendingRequests"),
                SentRequests = GetInt(reader, "SentRequests"),
                BlockedUsers = GetInt(reader, "BlockedUsers")
            },
            new SqlParameter("@UserId", userId));

        var summary = counts.FirstOrDefault();

        return new FriendsSummaryDto
        {
            TotalFriends = summary?.TotalFriends ?? 0,
            PendingRequests = summary?.PendingRequests ?? 0,
            SentRequests = summary?.SentRequests ?? 0,
            BlockedUsers = summary?.BlockedUsers ?? 0,
            RecentFriends = await GetUserFriendsAsync(userId, "Accepted"),
            PendingFriendRequests = await GetUserFriendsAsync(userId, "Pending")
        };
    }

    // Friend Invitations

    public async Task<Guid> SendInvitationAsync(Guid inviterId, string inviteeEmail, string? inviteePhone = null, string? message = null)
    {
        var id = Guid.NewGuid();
        var invitationCode = GenerateInvitationCode();
        var expiresAt = DateTime.UtcNow.AddDays(30);

        const string sql = @"
            INSERT INTO FriendInvitation (Id, InviterId, InviteeEmail, InviteePhone, InvitationCode,
                                         InvitationMessage, Status, SentAt, ExpiresAt)
            VALUES (@Id, @InviterId, @InviteeEmail, @InviteePhone, @InvitationCode,
                    @InvitationMessage, 'Sent', GETUTCDATE(), @ExpiresAt)";

        await ExecuteNonQueryAsync(sql,
            new SqlParameter("@Id", id),
            new SqlParameter("@InviterId", inviterId),
            new SqlParameter("@InviteeEmail", inviteeEmail),
            new SqlParameter("@InviteePhone", (object?)inviteePhone ?? DBNull.Value),
            new SqlParameter("@InvitationCode", invitationCode),
            new SqlParameter("@InvitationMessage", (object?)message ?? DBNull.Value),
            new SqlParameter("@ExpiresAt", expiresAt));

        return id;
    }

    public async Task<FriendInvitationDto?> GetInvitationByCodeAsync(string invitationCode)
    {
        const string sql = @"
            SELECT fi.Id, fi.InviterId, u.Email AS InviterName, fi.InviteeEmail, fi.InviteePhone,
                   fi.InvitationCode, fi.InvitationMessage, fi.Status, fi.SentAt,
                   fi.AcceptedAt, fi.AcceptedByUserId, fi.ExpiresAt
            FROM FriendInvitation fi
            INNER JOIN [User] u ON fi.InviterId = u.Id
            WHERE fi.InvitationCode = @InvitationCode";

        var results = await ExecuteReaderAsync(
            sql,
            reader => new FriendInvitationDto
            {
                Id = GetGuid(reader, "Id"),
                InviterId = GetGuid(reader, "InviterId"),
                InviterName = GetString(reader, "InviterName"),
                InviteeEmail = GetString(reader, "InviteeEmail") ?? string.Empty,
                InviteePhone = GetString(reader, "InviteePhone"),
                InvitationCode = GetString(reader, "InvitationCode") ?? string.Empty,
                InvitationMessage = GetString(reader, "InvitationMessage"),
                Status = GetString(reader, "Status") ?? string.Empty,
                SentAt = GetDateTime(reader, "SentAt") ?? DateTime.UtcNow,
                AcceptedAt = GetDateTime(reader, "AcceptedAt"),
                AcceptedByUserId = GetGuidNullable(reader, "AcceptedByUserId"),
                ExpiresAt = GetDateTime(reader, "ExpiresAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@InvitationCode", invitationCode));

        return results.FirstOrDefault();
    }

    public async Task<bool> AcceptInvitationAsync(string invitationCode, Guid acceptingUserId)
    {
        var invitation = await GetInvitationByCodeAsync(invitationCode);
        if (invitation == null || invitation.Status != "Sent" || invitation.ExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        // Update invitation
        const string updateSql = @"
            UPDATE FriendInvitation
            SET Status = 'Accepted',
                AcceptedAt = GETUTCDATE(),
                AcceptedByUserId = @AcceptingUserId
            WHERE InvitationCode = @InvitationCode
              AND Status = 'Sent'
              AND ExpiresAt >= GETUTCDATE()";

        var rowsAffected = await ExecuteNonQueryAsync(updateSql,
            new SqlParameter("@InvitationCode", invitationCode),
            new SqlParameter("@AcceptingUserId", acceptingUserId));

        // Create friendship
        if (rowsAffected > 0)
        {
            await SendFriendRequestAsync(invitation.InviterId, acceptingUserId, "Accepted invitation");
            await AcceptFriendRequestAsync((await GetFriendshipAsync(invitation.InviterId, acceptingUserId))!.Id, acceptingUserId);
        }

        return rowsAffected > 0;
    }

    public async Task<List<FriendInvitationDto>> GetUserInvitationsAsync(Guid userId)
    {
        const string sql = @"
            SELECT fi.Id, fi.InviterId, u.Email AS InviterName, fi.InviteeEmail, fi.InviteePhone,
                   fi.InvitationCode, fi.InvitationMessage, fi.Status, fi.SentAt,
                   fi.AcceptedAt, fi.AcceptedByUserId, fi.ExpiresAt
            FROM FriendInvitation fi
            INNER JOIN [User] u ON fi.InviterId = u.Id
            WHERE fi.InviterId = @UserId
            ORDER BY fi.SentAt DESC";

        return await ExecuteReaderAsync(
            sql,
            reader => new FriendInvitationDto
            {
                Id = GetGuid(reader, "Id"),
                InviterId = GetGuid(reader, "InviterId"),
                InviterName = GetString(reader, "InviterName"),
                InviteeEmail = GetString(reader, "InviteeEmail") ?? string.Empty,
                InviteePhone = GetString(reader, "InviteePhone"),
                InvitationCode = GetString(reader, "InvitationCode") ?? string.Empty,
                InvitationMessage = GetString(reader, "InvitationMessage"),
                Status = GetString(reader, "Status") ?? string.Empty,
                SentAt = GetDateTime(reader, "SentAt") ?? DateTime.UtcNow,
                AcceptedAt = GetDateTime(reader, "AcceptedAt"),
                AcceptedByUserId = GetGuidNullable(reader, "AcceptedByUserId"),
                ExpiresAt = GetDateTime(reader, "ExpiresAt") ?? DateTime.UtcNow
            },
            new SqlParameter("@UserId", userId));
    }

    private string GenerateInvitationCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude similar-looking characters
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
