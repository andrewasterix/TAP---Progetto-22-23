using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace TAP22_23_AuctionSite.Data {
    internal class Session : ISession {
        
        /* SESSION DATA */
        [Key] public string Id { get; set; } = null!;

        public DateTime DbValidUntil { get; set; }
        [NotMapped] public DateTime ValidUntil {
            get {
                using (var c = new AuctionDbContext(_connectionString))
                {
                    var thisSession = c.Sessions.SingleOrDefault(s => s.Id == Id);

                    return thisSession == null ? DbValidUntil : thisSession.DbValidUntil;
                }
            }
            set => DbValidUntil = value;
        }

        /* USER DATA */
        public User? Owner { get; set; }

        public int UserId { get; set; }

        /* SITE DATA */
        public Site? Site { get; set; }

        public int SiteId { get; set; }

        [NotMapped] public IUser User { get; set; } = null!;

        [NotMapped] private readonly string _connectionString = null!;
        [NotMapped] private readonly IAlarmClock _alarmClock = null!;

        #region Costructor
        private Session() { }

        public Session(int siteId, int userId, User user, DateTime validUntil, string connectionString, IAlarmClock alarmClock) {
            Id = userId.ToString();
            _connectionString = connectionString;
            SiteId = siteId;
            UserId = userId;
            ValidUntil = validUntil;
            Id = userId.ToString();
            User = user;
            _alarmClock = alarmClock;
        }
        #endregion

        #region ISession properties
        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice) {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SESSION has been DELETED");

            if (_alarmClock.Now > ValidUntil)
                throw new AuctionSiteInvalidOperationException("SESSION is EXPIRED");

            if (description == null)
                throw new AuctionSiteArgumentNullException("AUCTION->Description == NULL");

            if (description == "")
                throw new AuctionSiteArgumentException("AUCTION Description == EMPTY");

            if (startingPrice < 0)
                throw new AuctionSiteArgumentOutOfRangeException("AUCTION->StartingPrice < 0");

            if (endsOn < _alarmClock.Now)
                throw new AuctionSiteUnavailableTimeMachineException("ENDon is PASSED");
            #endregion

            using var c = new AuctionDbContext(_connectionString);
            var user = c.Users.SingleOrDefault(u => u.SiteId == SiteId && u.Username == User.Username);

            if (user == null)
                throw new AuctionSiteUnavailableDbException("USER == NULL");

            var auction = new Auction(0, user.UserId,
                new User(SiteId, user.Username, user.Password, _connectionString, _alarmClock),
                SiteId, description, endsOn, startingPrice, startingPrice, _connectionString, _alarmClock);

            c.Auctions.Add(auction);

            var site = c.Sites.SingleOrDefault(s => s.SiteId == SiteId);
            ValidUntil = _alarmClock.Now.AddSeconds(site!.SessionExpirationInSeconds);

            var session = c.Sessions.SingleOrDefault(s => s.Id == Id);
            session!.DbValidUntil = _alarmClock.Now.AddSeconds(site!.SessionExpirationInSeconds);

            c.SaveChanges();

            return new Auction(auction.Id, auction.SellerUserId,
                new User(user.SiteId, user.Username, user.Password, _connectionString, _alarmClock),
                auction.SiteId, description, endsOn, startingPrice, startingPrice, _connectionString, _alarmClock); ;
        }

        public void Logout() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("SESSION has been DELETED");
            #endregion

            using var c = new AuctionDbContext(_connectionString);
            var thisSession = c.Sessions.SingleOrDefault(s => s.Id == Id);

            if (thisSession == null) return;
            c.Remove(thisSession);
            c.SaveChanges();
        }
        #endregion

        #region Private Properties
        private bool IsDeleted() {
            using (var c = new AuctionDbContext(_connectionString))
            {
                var thisSession = c.Sessions.SingleOrDefault(s => s.Id == Id);
                return thisSession == null;
            }
        }
        #endregion

        #region Overrides
        public override bool Equals(object? obj) {
            var item = obj as Session;
            return item != null && Id.Equals(item.Id);
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }


        #endregion
    }
}
