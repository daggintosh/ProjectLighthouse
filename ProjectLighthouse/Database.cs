using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LBPUnion.ProjectLighthouse.Administration;
using LBPUnion.ProjectLighthouse.Administration.Reports;
using LBPUnion.ProjectLighthouse.Configuration;
using LBPUnion.ProjectLighthouse.Extensions;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Levels;
using LBPUnion.ProjectLighthouse.Levels.Categories;
using LBPUnion.ProjectLighthouse.PlayerData;
using LBPUnion.ProjectLighthouse.PlayerData.Profiles;
using LBPUnion.ProjectLighthouse.PlayerData.Profiles.Email;
using LBPUnion.ProjectLighthouse.PlayerData.Reviews;
using LBPUnion.ProjectLighthouse.Tickets;
using LBPUnion.ProjectLighthouse.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse;

public class Database : DbContext
{
    public DbSet<CompletedMigration> CompletedMigrations { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Slot> Slots { get; set; }
    public DbSet<QueuedLevel> QueuedLevels { get; set; }
    public DbSet<HeartedLevel> HeartedLevels { get; set; }
    public DbSet<HeartedProfile> HeartedProfiles { get; set; }
    public DbSet<HeartedPlaylist> HeartedPlaylists { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<GameToken> GameTokens { get; set; }
    public DbSet<WebToken> WebTokens { get; set; }
    public DbSet<Score> Scores { get; set; }
    public DbSet<PhotoSubject> PhotoSubjects { get; set; }
    public DbSet<Photo> Photos { get; set; }
    public DbSet<LastContact> LastContacts { get; set; }
    public DbSet<VisitedLevel> VisitedLevels { get; set; }
    public DbSet<RatedLevel> RatedLevels { get; set; }
    public DbSet<AuthenticationAttempt> AuthenticationAttempts { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<RatedReview> RatedReviews { get; set; }
    public DbSet<DatabaseCategory> CustomCategories { get; set; }
    public DbSet<Reaction> Reactions { get; set; }
    public DbSet<GriefReport> Reports { get; set; }
    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }
    public DbSet<EmailSetToken> EmailSetTokens { get; set; }
    public DbSet<ModerationCase> Cases { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<RegistrationToken> RegistrationTokens { get; set; }
    public DbSet<APIKey> APIKeys { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<News> News { get; set; }
    public DbSet<Activity> Activity { get; set; }
    public DbSet<ActivitySubject> ActivitySubject { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseMySql(ServerConfiguration.Instance.DbConnectionString, MySqlServerVersion.LatestSupportedServerVersion);

#nullable enable
    public async Task<User> CreateUser(string username, string password, string? emailAddress = null)
    {
        if (!password.StartsWith('$')) throw new ArgumentException(nameof(password) + " is not a BCrypt hash");

        // 16 is PSN max, 3 is PSN minimum
        if (!ServerStatics.IsUnitTesting || !username.StartsWith("unitTestUser"))
        {
            if (username.Length > 16 || username.Length < 3) throw new ArgumentException(nameof(username) + " is either too long or too short");

            Regex regex = new("^[a-zA-Z0-9_.-]*$");

            if (!regex.IsMatch(username)) throw new ArgumentException(nameof(username) + " does not match the username regex");
        }

        User? user = await this.Users.Where(u => u.Username == username).FirstOrDefaultAsync();
        if (user != null) return user;

        Location l = new(); // store to get id after submitting
        this.Locations.Add(l); // add to table
        await this.SaveChangesAsync(); // saving to the database returns the id and sets it on this entity

        user = new User
        {
            Username = username,
            Password = password,
            LocationId = l.Id,
            Biography = "",
            EmailAddress = emailAddress,
        };
        this.Users.Add(user);

        await this.SaveChangesAsync();

        if (emailAddress != null && ServerConfiguration.Instance.Mail.MailEnabled)
        {
            string body = "An account for Project Lighthouse has been registered with this email address.\n\n" +
                          $"You can login at {ServerConfiguration.Instance.ExternalUrl}.";

            SMTPHelper.SendEmail(emailAddress, "Project Lighthouse Account Created: " + username, body);
        }

        return user;
    }

    public async Task<GameToken?> AuthenticateUser(NPTicket npTicket, string userLocation)
    {
        User? user = await this.Users.FirstOrDefaultAsync(u => u.Username == npTicket.Username);
        if (user == null) return null;

        GameToken gameToken = new()
        {
            UserToken = CryptoHelper.GenerateAuthToken(),
            User = user,
            UserId = user.UserId,
            UserLocation = userLocation,
            GameVersion = npTicket.GameVersion,
            Platform = npTicket.Platform,
            // we can get away with a low expiry here since LBP will just get a new token everytime it gets 403'd
            ExpiresAt = DateTime.Now + TimeSpan.FromHours(1),
        };

        this.GameTokens.Add(gameToken);
        await this.SaveChangesAsync();

        return gameToken;
    }

    #region Hearts & Queues

    public async Task<bool> RateComment(int userId, int commentId, int rating)
    {
        Comment? comment = await this.Comments.FirstOrDefaultAsync(c => commentId == c.CommentId);

        if (comment == null) return false;

        if (comment.PosterUserId == userId) return false;

        Reaction? reaction = await this.Reactions.FirstOrDefaultAsync(r => r.UserId == userId && r.TargetId == commentId);
        if (reaction == null)
        {
            Reaction newReaction = new()
            {
                UserId = userId,
                TargetId = commentId,
                Rating = 0,
            };
            this.Reactions.Add(newReaction);
            await this.SaveChangesAsync();
            reaction = newReaction;
        }

        rating = Math.Clamp(rating, -1, 1);

        int oldRating = reaction.Rating;
        if (oldRating == rating) return true;

        reaction.Rating = rating;
        // if rating changed then we count the number of reactions to ensure accuracy
        List<Reaction> reactions = await this.Reactions.Where(c => c.TargetId == commentId).ToListAsync();
        int yay = 0;
        int boo = 0;
        foreach (Reaction r in reactions)
        {
            switch (r.Rating)
            {
                case -1:
                    boo++;
                    break;
                case 1:
                    yay++;
                    break;
            }
        }

        comment.ThumbsDown = boo;
        comment.ThumbsUp = yay;
        await this.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PostComment(int userId, int targetId, CommentType type, string message)
    {
        if (message.Length > 100) return false;

        if (type == CommentType.Profile)
        {
            User? targetUser = await this.Users.FirstOrDefaultAsync(u => u.UserId == targetId);
            if (targetUser == null) return false;
        }
        else
        {
            Slot? targetSlot = await this.Slots.FirstOrDefaultAsync(u => u.SlotId == targetId);
            if (targetSlot == null) return false;
        }

        Comment newComment = new Comment
        {
            PosterUserId = userId,
            TargetId = targetId,
            Type = type,
            Message = message,
            Timestamp = TimeHelper.UnixTimeMilliseconds(),
        };

        this.Comments.Add(newComment);
        await this.SaveChangesAsync();
        if (type == CommentType.Profile)
        {
            await this.CreateActivitySubject(ActivityType.Profile, userId, targetId, EventType.CommentUser, newComment.CommentId);
        }
        else
        {
            await this.CreateActivitySubject(ActivityType.Level, userId, targetId, EventType.CommentLevel, newComment.CommentId);
        }

        return true;
    }

    public async Task HeartUser(int userId, User heartedUser)
    {
        HeartedProfile? heartedProfile = await this.HeartedProfiles.FirstOrDefaultAsync(q => q.UserId == userId && q.HeartedUserId == heartedUser.UserId);
        if (heartedProfile != null) return;

        this.HeartedProfiles.Add
        (
            new HeartedProfile
            {
                HeartedUserId = heartedUser.UserId,
                UserId = userId,
            }
        );

        await this.SaveChangesAsync();
        await this.CreateActivitySubject(ActivityType.Profile, userId, heartedUser.UserId, EventType.HeartUser);
    }

    public async Task UnheartUser(int userId, User heartedUser)
    {
        HeartedProfile? heartedProfile = await this.HeartedProfiles.FirstOrDefaultAsync(q => q.UserId == userId && q.HeartedUserId == heartedUser.UserId);
        if (heartedProfile != null) this.HeartedProfiles.Remove(heartedProfile);

        ActivitySubject? subject = await this.ActivitySubject.FirstOrDefaultAsync(a => a.ActivityType == ActivityType.Profile && a.ActorId == userId && a.ActivityObjectId == heartedUser.UserId);
        if (subject != null) await this.DeleteActivitySubject(subject);

        await this.SaveChangesAsync();
    }

    public async Task HeartPlaylist(int userId, Playlist heartedPlaylist)
    {
        HeartedPlaylist? heartedList = await this.HeartedPlaylists.FirstOrDefaultAsync(p => p.UserId == userId && p.PlaylistId == heartedPlaylist.PlaylistId);
        if (heartedList != null) return;

        this.HeartedPlaylists.Add(new HeartedPlaylist()
        {
            PlaylistId = heartedPlaylist.PlaylistId,
            UserId = userId,
        });

        await this.SaveChangesAsync();
    }

    public async Task UnheartPlaylist(int userId, Playlist heartedPlaylist)
    {
        HeartedPlaylist? heartedList = await this.HeartedPlaylists.FirstOrDefaultAsync(p => p.UserId == userId && p.PlaylistId == heartedPlaylist.PlaylistId);
        if (heartedList != null) this.HeartedPlaylists.Remove(heartedList);

        await this.SaveChangesAsync();
    }

    public async Task HeartLevel(int userId, Slot heartedSlot)
    {
        HeartedLevel? heartedLevel = await this.HeartedLevels.FirstOrDefaultAsync(q => q.UserId == userId && q.SlotId == heartedSlot.SlotId);
        if (heartedLevel != null) return;

        this.HeartedLevels.Add
        (
            new HeartedLevel
            {
                SlotId = heartedSlot.SlotId,
                UserId = userId,
            }
        );

        await this.SaveChangesAsync();
        await this.CreateActivitySubject(ActivityType.Level, userId, heartedSlot.SlotId, EventType.HeartLevel);
    }

    public async Task UnheartLevel(int userId, Slot heartedSlot)
    {
        HeartedLevel? heartedLevel = await this.HeartedLevels.FirstOrDefaultAsync(q => q.UserId == userId && q.SlotId == heartedSlot.SlotId);
        if (heartedLevel != null) this.HeartedLevels.Remove(heartedLevel);

        ActivitySubject? subject = await this.ActivitySubject.FirstOrDefaultAsync(a => a.ActivityType == ActivityType.Level && a.ActorId == userId && a.ActivityObjectId == heartedSlot.SlotId);
        if (subject != null) await this.DeleteActivitySubject(subject);
        
        await this.SaveChangesAsync();
    }

    public async Task QueueLevel(int userId, Slot queuedSlot)
    {
        QueuedLevel? queuedLevel = await this.QueuedLevels.FirstOrDefaultAsync(q => q.UserId == userId && q.SlotId == queuedSlot.SlotId);
        if (queuedLevel != null) return;

        this.QueuedLevels.Add
        (
            new QueuedLevel
            {
                SlotId = queuedSlot.SlotId,
                UserId = userId,
            }
        );

        await this.SaveChangesAsync();
    }

    public async Task UnqueueLevel(int userId, Slot queuedSlot)
    {
        QueuedLevel? queuedLevel = await this.QueuedLevels.FirstOrDefaultAsync(q => q.UserId == userId && q.SlotId == queuedSlot.SlotId);
        if (queuedLevel != null) this.QueuedLevels.Remove(queuedLevel);

        await this.SaveChangesAsync();
    }

    #endregion

    #region Game Token Shenanigans

    public async Task<string> UsernameFromGameToken(GameToken? token)
    {
        if (token == null) return "";

        return await this.Users.Where(u => u.UserId == token.UserId).Select(u => u.Username).FirstAsync();
    }

    public async Task<User?> UserFromMMAuth(string authToken, bool allowUnapproved = false)
    {
        if (ServerStatics.IsUnitTesting) allowUnapproved = true;
        GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == authToken);

        if (token == null) return null;
        if (!allowUnapproved && !token.Approved) return null;

        if (DateTime.Now > token.ExpiresAt)
        {
            this.Remove(token);
            await this.SaveChangesAsync();
            return null;
        }

        return await this.Users.FirstOrDefaultAsync(u => u.UserId == token.UserId);
    }

    public async Task<User?> UserFromGameToken
        (GameToken gameToken, bool allowUnapproved = false)
        => await this.UserFromMMAuth(gameToken.UserToken, allowUnapproved);

    public async Task<User?> UserFromGameRequest(HttpRequest request, bool allowUnapproved = false)
    {
        if (ServerStatics.IsUnitTesting) allowUnapproved = true;
        if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

        return await this.UserFromMMAuth(mmAuth, allowUnapproved);
    }

    public async Task<GameToken?> GameTokenFromRequest(HttpRequest request, bool allowUnapproved = false)
    {
        if (ServerStatics.IsUnitTesting) allowUnapproved = true;
        if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

        GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == mmAuth);

        if (token == null) return null;
        if (!allowUnapproved && !token.Approved) return null;

        if (DateTime.Now > token.ExpiresAt)
        {
            this.Remove(token);
            await this.SaveChangesAsync();
            return null;
        }

        return token;
    }

    public async Task<(User, GameToken)?> UserAndGameTokenFromRequest(HttpRequest request, bool allowUnapproved = false)
    {
        if (ServerStatics.IsUnitTesting) allowUnapproved = true;
        if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

        GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == mmAuth);
        if (token == null) return null;
        if (!allowUnapproved && !token.Approved) return null;

        if (DateTime.Now > token.ExpiresAt)
        {
            this.Remove(token);
            await this.SaveChangesAsync();
            return null;
        }

        User? user = await this.Users.FirstOrDefaultAsync(u => u.UserId == token.UserId);

        if (user == null) return null;

        return (user, token);
    }

    #endregion

    #region Web Token Shenanigans

    public async Task<string> UsernameFromWebToken(WebToken? token)
    {
        if (token == null) return "";

        return await this.Users.Where(u => u.UserId == token.UserId).Select(u => u.Username).FirstAsync();
    }

    public User? UserFromLighthouseToken(string lighthouseToken)
    {
        WebToken? token = this.WebTokens.FirstOrDefault(t => t.UserToken == lighthouseToken);
        if (token == null) return null;

        if (DateTime.Now > token.ExpiresAt)
        {
            this.Remove(token);
            this.SaveChanges();
            return null;
        }

        return this.Users.Include(u => u.Location).FirstOrDefault(u => u.UserId == token.UserId);
    }

    public User? UserFromWebRequest(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue("LighthouseToken", out string? lighthouseToken) || lighthouseToken == null) return null;

        return this.UserFromLighthouseToken(lighthouseToken);
    }

    public WebToken? WebTokenFromRequest(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue("LighthouseToken", out string? lighthouseToken) || lighthouseToken == null) return null;

        WebToken? token = this.WebTokens.FirstOrDefault(t => t.UserToken == lighthouseToken);
        if (token == null) return null;

        if (DateTime.Now > token.ExpiresAt)
        {
            this.Remove(token);
            this.SaveChanges();
            return null;
        }

        return token;
    }

    public async Task<User?> UserFromPasswordResetToken(string resetToken)
    {
        PasswordResetToken? token = await this.PasswordResetTokens.FirstOrDefaultAsync(token => token.ResetToken == resetToken);
        if (token == null)
        {
            return null;
        }

        if (token.Created < DateTime.Now.AddHours(-1)) // if token is expired
        {
            this.PasswordResetTokens.Remove(token);
            await this.SaveChangesAsync();
            return null;
        }

        return await this.Users.FirstOrDefaultAsync(user => user.UserId == token.UserId);
    }

    public bool IsRegistrationTokenValid(string tokenString)
    {
        RegistrationToken? token = this.RegistrationTokens.FirstOrDefault(t => t.Token == tokenString);

        if (token == null) return false;

        if (token.Created < DateTime.Now.AddDays(-7)) // if token is expired
        {
            this.RegistrationTokens.Remove(token);
            this.SaveChanges();
            return false;
        }

        return true;
    }

    public async Task CreateActivity
    (
        int destinationId,
        ActivityType category,
        long? extraData = null // Sneaking data, used in Team Picks 
    )
    {
        Activity? activity = await this.Activity.AsAsyncEnumerable().FirstOrDefaultAsync(a => destinationId == a.ActivityTargetId && category == a.ActivityType);
        if (activity != null) return;

        Activity newActivitySlot = new Activity
                                    {
                                        ActivityType = category,
                                        ActivityTargetId = destinationId
                                    };
        if (extraData != null) newActivitySlot.ExtrasCollection = $"{extraData}";

        this.Activity.Add(newActivitySlot);

        await this.SaveChangesAsync();
    }

    public async Task InsertEventToActivity(int targetId, ActivityType activityType, int eventId, int actorId)
    {
        Activity? activity = await this.Activity.AsAsyncEnumerable().FirstOrDefaultAsync(a => targetId == a.ActivityTargetId && activityType == a.ActivityType);
        if (activity == null) {
            await CreateActivity(targetId, activityType); 
            activity = await this.Activity.AsAsyncEnumerable().FirstOrDefaultAsync(a => targetId == a.ActivityTargetId && activityType == a.ActivityType);
        }
        if (activity == null) return;

        if(!activity.Extras.Contains(actorId)) {
            activity.ExtrasCollection += "," + actorId;
        }

        if (activity.ExtrasCollection[0] == ","[0]) activity.ExtrasCollection = activity.ExtrasCollection.Remove(0, 1);

        await this.SaveChangesAsync();
    }

    public async Task DeleteActivity
    (
        int targetId,
        ActivityType targetCategory
    )
    {
        Activity? activity = await this.Activity.FirstOrDefaultAsync(a => targetId == a.ActivityTargetId && targetCategory == a.ActivityType);
        if (activity == null) return;

        IEnumerable<ActivitySubject> associatedSubjects = this.ActivitySubject.Where(a => a.ActivityType == targetCategory && a.ActivityObjectId == targetId);

        foreach(ActivitySubject subject in associatedSubjects)
        {
            this.ActivitySubject.Remove(subject);
        }

        this.Activity.Remove(activity);

        await this.SaveChangesAsync();
    }

    public async Task CreateActivitySubject
    (
        ActivityType activityType,
        int sourceId,
        int destinationId,
        EventType eventType,
        int interact1 = 0,
        long interact2 = 0
    )
    {
        if (activityType == ActivityType.Level)
        {
            Slot? slot = await this.Slots.FirstOrDefaultAsync(s => s.SlotId == destinationId);
            if 
            (
                slot != null && (
                    slot.Type == SlotType.Pod || 
                    slot.Type == SlotType.Developer || 
                    slot.Type == SlotType.Local ||
                    slot.Type == SlotType.Moon || 
                    slot.Type == SlotType.DLC
                )
            ) return; // Do not log offline levels.
        }
        ActivitySubject? activitySubject = await this.ActivitySubject.FirstOrDefaultAsync(a =>
            a.ActivityType == activityType &&
            a.ActorId == sourceId &&
            a.ActivityObjectId == destinationId &&
            a.EventType == eventType
        );
        if (activitySubject != null)
        {
            activitySubject.Interaction = interact1;
            activitySubject.Interaction2 = interact2;
            activitySubject.EventTimestamp = TimeHelper.UnixTimeMilliseconds();
            await this.SaveChangesAsync();
            return;
        }

        ActivitySubject newActivitySubject = new ActivitySubject
        {
            EventTimestamp = TimeHelper.UnixTimeMilliseconds(),
            ActivityType = activityType, // Activity type
            ActorId = sourceId,
            ActivityObjectId = destinationId,
            EventType = eventType, // Event type
            Interaction = interact1,
            Interaction2 = interact2
        };

        this.ActivitySubject.Add(newActivitySubject);

        await this.SaveChangesAsync();

        await this.InsertEventToActivity(newActivitySubject.ActivityObjectId, newActivitySubject.ActivityType, newActivitySubject.SubjectId, newActivitySubject.ActorId);
    }

    public async Task DeleteActivitySubject(ActivitySubject subject)
    {
        this.ActivitySubject.Remove(subject);

        await this.SaveChangesAsync();
    }

    public async Task RemoveExpiredTokens()
    {
        foreach (GameToken token in await this.GameTokens.Where(t => DateTime.Now > t.ExpiresAt).ToListAsync())
        {
            User? user = await this.Users.FirstOrDefaultAsync(u => u.UserId == token.UserId);
            if (user != null) user.LastLogout = TimeHelper.TimestampMillis;
            this.GameTokens.Remove(token);
        }
        this.WebTokens.RemoveWhere(t => DateTime.Now > t.ExpiresAt);
        this.EmailVerificationTokens.RemoveWhere(t => DateTime.Now > t.ExpiresAt);
        this.EmailSetTokens.RemoveWhere(t => DateTime.Now > t.ExpiresAt);

        await this.SaveChangesAsync();
    }

    public async Task RemoveRegistrationToken(string tokenString)
    {
        RegistrationToken? token = await this.RegistrationTokens.FirstOrDefaultAsync(t => t.Token == tokenString);

        if (token == null) return;

        this.RegistrationTokens.Remove(token);
        await this.SaveChangesAsync();
    }

    #endregion

    public async Task<Photo?> PhotoFromSubject(PhotoSubject subject)
        => await this.Photos.FirstOrDefaultAsync(p => p.PhotoSubjectIds.Contains(subject.PhotoSubjectId.ToString()));
    public async Task RemoveUser(User? user)
    {
        if (user == null) return;
        if (user.Username.Length == 0) return; // don't delete the placeholder user

        if (user.Location != null) this.Locations.Remove(user.Location);
        LastContact? lastContact = await this.LastContacts.FirstOrDefaultAsync(l => l.UserId == user.UserId);
        if (lastContact != null) this.LastContacts.Remove(lastContact);

        foreach (Slot slot in this.Slots.Where(s => s.CreatorId == user.UserId)) await this.RemoveSlot(slot, false);

        this.AuthenticationAttempts.RemoveRange(this.AuthenticationAttempts.Include(a => a.GameToken).Where(a => a.GameToken.UserId == user.UserId));
        this.HeartedProfiles.RemoveRange(this.HeartedProfiles.Where(h => h.UserId == user.UserId));
        this.PhotoSubjects.RemoveRange(this.PhotoSubjects.Where(s => s.UserId == user.UserId));
        this.HeartedLevels.RemoveRange(this.HeartedLevels.Where(h => h.UserId == user.UserId));
        this.VisitedLevels.RemoveRange(this.VisitedLevels.Where(v => v.UserId == user.UserId));
        this.RatedReviews.RemoveRange(this.RatedReviews.Where(r => r.UserId == user.UserId));
        this.QueuedLevels.RemoveRange(this.QueuedLevels.Where(q => q.UserId == user.UserId));
        this.RatedLevels.RemoveRange(this.RatedLevels.Where(r => r.UserId == user.UserId));
        this.GameTokens.RemoveRange(this.GameTokens.Where(t => t.UserId == user.UserId));
        this.WebTokens.RemoveRange(this.WebTokens.Where(t => t.UserId == user.UserId));
        this.Reactions.RemoveRange(this.Reactions.Where(p => p.UserId == user.UserId));
        this.Comments.RemoveRange(this.Comments.Where(c => c.PosterUserId == user.UserId));
        this.Reviews.RemoveRange(this.Reviews.Where(r => r.ReviewerId == user.UserId));
        this.Photos.RemoveRange(this.Photos.Where(p => p.CreatorId == user.UserId));
        this.Activity.RemoveRange(this.Activity.Where(a => a.ActivityType == ActivityType.Profile && a.ActivityTargetId == user.UserId));

        this.Users.Remove(user);

        await this.SaveChangesAsync();
    }

    public async Task RemoveSlot(Slot slot, bool saveChanges = true)
    {
        if (slot.Location != null) this.Locations.Remove(slot.Location);
        this.Slots.Remove(slot);

        if (saveChanges) await this.SaveChangesAsync();
    }
#nullable disable
}
