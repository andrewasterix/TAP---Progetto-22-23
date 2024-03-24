using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace TAP22_23_AuctionSite.Data {
    [Index(nameof(Name), IsUnique = true, Name = "SiteUnique")]
    internal class Site : ISite {
        
        /* SITE DATA */
        [Key] public int SiteId { get; set; }
        [MinLength(DomainConstraints.MaxSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { get; } = null!;

        /* SESSION DATA */
        [Range(1, int.MaxValue)] 
        public int SessionExpirationInSeconds { get; set; }
        public List<Session> Sessions { get; set; } = new();

        /* AUCTION DATA */
        [Range(double.Epsilon, double.PositiveInfinity)] 
        public double MinimumBidIncrement { get; set; }
        public List<Auction> Auctions { get; set; } = new();

        /* DATA */
        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; set; }

        [NotMapped] private readonly IAlarmClock _alarmClock = null!;
        [NotMapped] private readonly string _connectionString = null!;

        #region Constructor
        private Site() { }
        public Site(string name, int timeZone, int sessionExpirationInSeconds, double minimumBidIncrement, IAlarmClock alarmClock, string connectionString) {
            Name = name;
            Timezone = timeZone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            _alarmClock = alarmClock;
            _connectionString = connectionString;
        }
        #endregion

        #region ISite Properties
        public IEnumerable<IUser> ToyGetUsers() {

            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            #endregion

            var userList = new List<IUser>();

            using var conn = new AuctionDbContext(_connectionString);
            var users = conn.Users.Where(u => u.SiteId == SiteId).ToList();

            foreach (var user in users)
                userList.Add(new User(SiteId, user.Username, user.Password, _connectionString, _alarmClock));
            return userList;
        }

        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded) {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            #endregion

            var auctionsList = new List<IAuction>();

            using var conn = new AuctionDbContext(_connectionString);
            var auctions = conn.Auctions.Where(a => a.SiteId == SiteId).Include(a => a.SellerUser).ToList();

            if (onlyNotEnded) {
                foreach (var auction in auctions) {
                    if (auction.EndsOn >= _alarmClock.Now)
                        auctionsList.Add(new Auction(auction.Id, auction.SellerUserId, auction.SellerUser!, SiteId, auction.Description, auction.EndsOn, auction.ActualPrice,
                            auction.ActualPrice, _connectionString, _alarmClock) { TopAmount = auction.TopAmount });
                }
            } else {
                foreach (var auction in auctions)
                    auctionsList.Add(new Auction(auction.Id, auction.SellerUserId, auction.SellerUser!, SiteId, auction.Description, auction.EndsOn, auction.ActualPrice,
                        auction.ActualPrice, _connectionString, _alarmClock) { TopAmount = auction.TopAmount });
            }


            return auctionsList;
        }

        public IEnumerable<ISession> ToyGetSessions() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            #endregion

            var sessionList = new List<ISession>();

            using var conn = new AuctionDbContext(_connectionString);
            var sessions = conn.Sessions.Include(s => s.Owner).Where(s => s.SiteId == SiteId).ToList();

            foreach (var session in sessions)
                sessionList.Add(new Session(SiteId, session.UserId, session.Owner!, session.DbValidUntil, _connectionString, _alarmClock) { Id = session.Id });
            return sessionList;
        }

        public ISession? Login(string username, string password) {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            
            if (username == null)
                throw new AuctionSiteArgumentNullException("Username == NULL");

            if (password == null)
                throw new AuctionSiteArgumentNullException("Password == NULL");

            if (username.Length < DomainConstraints.MinUserName || username.Length > DomainConstraints.MaxUserName)
                throw new AuctionSiteArgumentException($"OUT of: {DomainConstraints.MinUserName} <= Username.Length <= {DomainConstraints.MaxUserName}");

            if (password.Length < DomainConstraints.MinUserPassword)
                throw new AuctionSiteArgumentException($"OUT of: Password.Length >= {DomainConstraints.MinUserPassword}");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);
            var user = conn.Users.Include(u => u.Session)
                .SingleOrDefault(u => u.Username == username && u.SiteId == SiteId && u.Password == password);

            if (user == null)
                return null;

            if (user.SessionId == null) {
                var session = new Session(SiteId, user.UserId,
                    new User(SiteId, username, password, _connectionString, _alarmClock),
                    _alarmClock.Now.AddSeconds(SessionExpirationInSeconds), _connectionString, _alarmClock) { Id = user.UserId.ToString() };

                user.SessionId = session.Id;

                conn.Sessions.Add(session);
                conn.SaveChanges();

                return session;
            } else {
                var session = user.Session;

                if (session == null)
                    throw new AuctionSiteUnavailableDbException("SESSION == NULL");

                session.ValidUntil = _alarmClock.Now.AddSeconds(SessionExpirationInSeconds);

                conn.Update(session);
                conn.SaveChanges();

                return new Session(SiteId, user.UserId,
                    new User(SiteId, username, password, _connectionString, _alarmClock),
                    session.DbValidUntil, _connectionString, _alarmClock) { Id = session.Id };
            }
        }

        public void CreateUser(string username, string password) {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");

            if (username == null)
                throw new AuctionSiteArgumentNullException("Username == NULL");

            if (password == null)
                throw new AuctionSiteArgumentNullException("Password == NULL");

            if (username.Length < DomainConstraints.MinUserName || username.Length > DomainConstraints.MaxUserName)
                throw new AuctionSiteArgumentException($"OUT of: {DomainConstraints.MinUserName} <= Username.Length <= {DomainConstraints.MaxUserName}");

            if (password.Length < DomainConstraints.MinUserPassword)
                throw new AuctionSiteArgumentException($"OUT of: Password.Length >= {DomainConstraints.MinUserPassword}");
            #endregion

            var newUser = new User(SiteId, username, password, _connectionString, _alarmClock);

            using var conn = new AuctionDbContext(_connectionString);
            var alreadyExistingUser = conn.Users.SingleOrDefault(u => u.Username == username);

            if (alreadyExistingUser != null)
                throw new AuctionSiteNameAlreadyInUseException(username);

            conn.Users.Add(newUser);
            conn.SaveChanges();
        }
        
        public void Delete() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);
            var site = conn.Sites.SingleOrDefault(s => s.Name == Name);

            if (site == null) return;
            conn.Remove(site);
            conn.SaveChanges();
        }

        public DateTime Now() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SITE has been DELETED");
            #endregion
            return _alarmClock.Now;
        }
        #endregion

        #region Private Properties
        private bool IsDeleted() {
            using (var conn = new AuctionDbContext(_connectionString))
            {
                var site = conn.Sites.SingleOrDefault(s => s.Name == Name);
                return site == null;
            }
        }
        #endregion

        #region Public Properties
        public void RemoveSessions() {
            using (var conn = new AuctionDbContext(_connectionString))
            {
                var sessionsToClean = conn.Sessions.Where(s => s.SiteId == SiteId && s.DbValidUntil <= _alarmClock.Now);

                conn.Sessions.RemoveRange(sessionsToClean);
                conn.SaveChanges();
            }
        }
        #endregion

        #region Overrides
        public override bool Equals(object? obj) {
            var item = obj as Site;
            return item != null && Name.Equals(item.Name);
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
        #endregion
    }
}