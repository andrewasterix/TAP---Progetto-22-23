using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

#nullable enable
namespace TAP22_23_AuctionSite.Data {
    internal class Auction : IAuction {

        /* AUCTION DATA */
        [Key] public int Id { get; set; }
        public DateTime EndsOn { get; set; }
        public string Description { get; set; } = null!;
        public double ActualPrice { get; set; } = 0;
        public double StartingPrice { get; set; } = 0;
        public double TopAmount { get; set; } = 0;

        /* USER DATA */
        public User? SellerUser { get; set; }
        public int SellerUserId { get; set; }
        public User? WinnerUser { get; set; }
        public int? WinnerUserId { get; set; }

        /* SITE DATA */
        public Site? Site { get; set; }
        public int SiteId { get; }
    
        /* DATA */
        [NotMapped] private int NumberOfBids { get; set; } = 0;
        [NotMapped] private ISession? FirstBidderSession { get; set; }
        [NotMapped] private ISession? LastBidderSession { get; set; }

        [NotMapped] public IUser Seller { get; set; } = null!;

        [NotMapped] private readonly string _connectionString = null!;
        [NotMapped] private readonly IAlarmClock _alarmClock = null!;

        #region Costructor
        private Auction() { }
        public Auction(int id, int sellerUserId, IUser seller, int siteId, string description, DateTime endsOn, double startingPrice, double actualPrice, string connectionString, IAlarmClock alarmClock) {
            Id = id;
            SellerUserId = sellerUserId;
            Seller = seller;
            SiteId = siteId;
            Description = description;
            EndsOn = endsOn;
            StartingPrice = startingPrice;
            ActualPrice = actualPrice;
            _connectionString = connectionString;
            _alarmClock = alarmClock;
        }
        #endregion

        #region IAuction Properties
        public double CurrentPrice() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("AUCTION has been DELETED.");
            #endregion

            if (NumberOfBids < 2 || (NumberOfBids == 2 && LastBidderSession == FirstBidderSession))
                return StartingPrice;

            using var conn = new AuctionDbContext(_connectionString);
            var thisAuction = conn.Auctions.SingleOrDefault(a => a.SiteId == SiteId && a.Id == Id);

            if (thisAuction == null)
                throw new AuctionSiteInvalidOperationException("AUCTION == NULL");

            return thisAuction.ActualPrice;
        }

        public IUser? CurrentWinner() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("AUCTION has been DELETED.");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);
            var thisAuction = conn.Auctions.Include(a => a.WinnerUser).SingleOrDefault(a => a.SiteId == SiteId && a.Id == Id);
            return thisAuction!.WinnerUser == null ? null : new User(SiteId, thisAuction.WinnerUser.Username, thisAuction.WinnerUser.Password, _connectionString, _alarmClock);
        }

        public bool Bid(ISession session, double offer) {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("AUCTION has been DELETED");

            if (EndsOn < _alarmClock.Now)
                throw new AuctionSiteInvalidOperationException("AUCTION does NOT EXIST anymore");

            if (offer < 0)
                throw new AuctionSiteArgumentOutOfRangeException("BIDDER->Offer < 0");

            if (offer < StartingPrice)
                return false;

            if (session == null)
                throw new AuctionSiteArgumentNullException("SESSION == NULL");

            using (var conn = new AuctionDbContext(_connectionString)) {
                var checkSession = conn.Sessions.SingleOrDefault(s => s.Id == session.Id);

                if (checkSession == null)
                    throw new AuctionSiteArgumentException("SESSION does not EXIST");
            }
            #endregion

            #region Check Bidder
            User? bidder;

            using (var conn = new AuctionDbContext(_connectionString)) {

                bidder = conn.Users.SingleOrDefault(u => u.Username == ((User)session.User).Username && u.SiteId == ((User)session.User).SiteId);
                if (bidder == null)
                    throw new AuctionSiteInvalidOperationException("USER->Bidder does not EXIST");
            }

            if (session.ValidUntil < _alarmClock.Now)
                throw new AuctionSiteArgumentException("SESSION is EXPIRED");

            if (bidder.Equals(Seller))
                throw new AuctionSiteArgumentException("USER->Bidder == SELLER");

            if (bidder.SiteId != SiteId)
                throw new AuctionSiteArgumentException("USER->Bidder->Site != Auction->Site");
            #endregion

            #region Check Auction
            Auction? auction;
            using (var conn = new AuctionDbContext(_connectionString)) {
                auction = conn.Auctions.Include(a => a.WinnerUser)
                    .Include(a => a.Site).SingleOrDefault(a => a.SiteId == SiteId && a.Id == Id);
            }

            if (auction == null)
                throw new AuctionSiteInvalidOperationException("AUCTION has been DELETED");

            if (auction.WinnerUserId == bidder.UserId && offer < auction.TopAmount + auction.Site!.MinimumBidIncrement)
                return false;

            if (auction.WinnerUserId != bidder.UserId && offer < auction.ActualPrice)
                return false;

            if (auction.WinnerUserId != bidder.UserId && offer < auction.ActualPrice + auction.Site!.MinimumBidIncrement && auction.TopAmount != 0)
                return false;
            #endregion

            using (var conn = new AuctionDbContext(_connectionString)) {
                var updateTimeSite = conn.Sites.SingleOrDefault(s => s.SiteId == SiteId);
                var updateTimeSession = conn.Sessions.SingleOrDefault(s => s.Id == session.Id);

                if (updateTimeSession == null)
                    throw new AuctionSiteArgumentException("BIDDER->Session == NULL");

                updateTimeSession.DbValidUntil = _alarmClock.Now.AddSeconds(updateTimeSite!.SessionExpirationInSeconds);

                conn.SaveChanges();
            }

            if (auction.TopAmount == 0) {
                auction.TopAmount = offer;
                auction.WinnerUserId = bidder.UserId;

                using (var conn = new AuctionDbContext(_connectionString)) {
                    conn.Auctions.Update(auction);
                    auction.WinnerUserId = bidder.UserId;
                    conn.SaveChanges();
                }

                SetSession(session);

                NumberOfBids++;

                return true;
            }

            if (bidder.Equals(auction.WinnerUser)) {
                auction.TopAmount = offer;
                using (var conn = new AuctionDbContext(_connectionString)) {
                    conn.Auctions.Update(auction);
                    conn.SaveChanges();
                }

                SetSession(session);

                NumberOfBids++;

                return true;
            }

            if (auction.TopAmount != 0 && !bidder.Equals(auction.WinnerUser) && offer > auction.TopAmount) {
                double minimum;

                using (var conn = new AuctionDbContext(_connectionString)) {
                    var site = conn.Sites.SingleOrDefault(s => s.SiteId == SiteId);

                    if (site == null)
                        throw new AuctionSiteArgumentException("SITE == NULL");

                    if (offer < auction.TopAmount + site.MinimumBidIncrement)
                        minimum = offer;
                    else
                        minimum = auction.TopAmount + site.MinimumBidIncrement;
                }

                auction.ActualPrice = minimum;
                auction.TopAmount = offer;
                auction.WinnerUserId = bidder.UserId;

                using (var conn = new AuctionDbContext(_connectionString)) {
                    conn.Auctions.Update(auction);
                    auction.WinnerUserId = bidder.UserId;
                    conn.SaveChanges();
                }

                SetSession(session);

                NumberOfBids++;

                return true;
            }

            double tempPrice = 0;

            if (auction.TopAmount < offer + auction.Site!.MinimumBidIncrement)
                tempPrice = auction.TopAmount;
            else
                tempPrice = offer + auction.Site!.MinimumBidIncrement;


            auction.ActualPrice = tempPrice;

            using (var conn = new AuctionDbContext(_connectionString)) {
                conn.Auctions.Update(auction);
                conn.SaveChanges();
            }

            SetSession(session);

            NumberOfBids++;

            return true;
        }

        public void Delete() {
            #region Sanity Checks
            if (IsDeleted())
                throw new AuctionSiteInvalidOperationException("AUCTION has been DELETED.");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);
            var thisAuction = conn.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteId == SiteId);

            if (thisAuction == null) return;
            conn.Remove(thisAuction);
            conn.SaveChanges();
        }
        #endregion

        #region Private Properties
        private void SetSession(ISession session) {
            if (NumberOfBids == 0)
                FirstBidderSession = session;
            else
                LastBidderSession = session;
        }

        private bool IsDeleted()
        {
            using var c = new AuctionDbContext(_connectionString);
            var auction = c.Auctions.SingleOrDefault(a => a.Id == Id && a.SiteId == SiteId);
            return auction == null;
        }
        #endregion

        #region Overrides
        public override bool Equals(object? obj) {
            var item = obj as Auction;

            if (item == null)
                return false;
            else
                return SiteId == item.SiteId && Id == item.Id;
        }

        public override int GetHashCode() {
            return HashCode.Combine(SiteId, Id);
        }
        #endregion
    }
}
