using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace TAP22_23_AuctionSite.Data {
    [Index(nameof(SiteId), nameof(Username), IsUnique = true, Name = "UserUnique")]
    internal class User : IUser {
        
        /* USER DATA */
        [Key] public int UserId { get; set; }
        
        [MinLength(DomainConstraints.MinUserName)] [MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; } = null!;

        public string Password { get; set; } = null!;

        /* SITE DATA */
        public int SiteId { get; }

        /* SESSION DATA */
        public string? SessionId { get; set; } = null;
        public Session? Session { get; set; }

        /* AUCTION DATA */
        public List<Auction> WinningAuctions { get; set; } = new();
        public List<Auction> SellingAuctions { get; set; } = new();

        [NotMapped] private readonly string _connectionString = null!;
        [NotMapped] private readonly IAlarmClock _alarmClock = null!;

        #region Costructor
        private User() { }
        public User(int siteId, string username, string password, string connectionString, IAlarmClock alarmClock) {
            Username = username;
            Password = password;
            SiteId = siteId;
            _connectionString = connectionString;
            _alarmClock = alarmClock;
        }
        #endregion

        #region IUser Properties
        public IEnumerable<IAuction> WonAuctions() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("USER has been DELETED");
            #endregion

            var auctionList = new List<IAuction>();

            using var conn = new AuctionDbContext(_connectionString);
            var winningAuctions = conn.Auctions.Where(a =>
                a.WinnerUser!.Username == Username && a.WinnerUser.SiteId == SiteId).Include(a => a.SellerUser).ToList();

            foreach (var auction in winningAuctions) {
                if (auction.EndsOn <= _alarmClock.Now)
                    auctionList.Add(new Auction(auction.Id, auction.SellerUserId, auction.SellerUser!, SiteId, auction.Description, auction.EndsOn, auction.StartingPrice,
                        auction.ActualPrice, _connectionString, _alarmClock) { TopAmount = auction.TopAmount });
            }

            return auctionList;
        }

        public void Delete() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("USER has been DELETED");
            #endregion


            using var conn = new AuctionDbContext(_connectionString);
            var winningAuctions = conn.Auctions.Where(a => a.SellerUserId == UserId);
            var owningAuctions = conn.Auctions.Where(a => a.SellerUserId == UserId);

            foreach (var auction in winningAuctions) {
                if (auction.EndsOn >= _alarmClock.Now)
                    throw new AuctionSiteInvalidOperationException("USER NOT DELETED, User is winning 1+ Auction");
                else {
                    auction.WinnerUser = null;
                    auction.WinnerUserId = null;
                }
            }

            foreach (var auction in owningAuctions) {
                if (auction.EndsOn >= _alarmClock.Now)
                    throw new AuctionSiteInvalidOperationException("USER NOT DELETED, User is owner of 1+ Auction");
                else
                    auction.Delete();
            }

            var user = conn.Users.SingleOrDefault(user => user.Username == Username && user.SiteId == SiteId);

            if (user == null) return;
            conn.Remove(user);
            conn.SaveChanges();
        }
        #endregion

        #region Private Properties
        private bool IsDeleted() {
            using var conn = new AuctionDbContext(_connectionString);
            var user = conn.Users.SingleOrDefault(u => u.SiteId == SiteId && u.Username == Username);
            return user == null;
        }
        #endregion

        #region Overrides
        public override bool Equals(object? obj) {
            var item = obj as User;

            if (item == null)
                return false;
            else
                return SiteId == item.SiteId && Username == item.Username;
        }

        public override int GetHashCode() {
            return HashCode.Combine(SiteId, Username);
        }
        #endregion
    }
}
