using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;

namespace TAP22_23_AuctionSite.Data {
    internal class AuctionDbContext : TapDbContext {
        public DbSet<Site> Sites { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<Auction> Auctions { get; set; } = null!;

        #region Costructor
        public AuctionDbContext(string connectionString) : base(new DbContextOptionsBuilder<AuctionDbContext>().UseSqlServer(connectionString).Options) { }
        #endregion

        #region Override
        public override int SaveChanges() {
            try {
                return base.SaveChanges();
            } catch (ArgumentException e) {
                throw new AuctionSiteUnavailableDbException("DB UNAVAILABLE", e);
            } catch (DbUpdateException e) {
                if (e is DbUpdateConcurrencyException) {
                    if (e.Entries[0].State == EntityState.Deleted)
                        throw new AuctionSiteInvalidOperationException("DELETED.ENTITY EXC", e);
                    else
                        throw new AuctionSiteConcurrentChangeException("CONCURRENT EXC", e);
                }

                switch ((e.InnerException as SqlException)!.Number) {
                    case < 54:
                        throw new AuctionSiteUnavailableDbException("DB UNAVAILABLE.", e);
                    case < 122:
                        throw new AuctionSiteInvalidOperationException("SIN TAX ERROR.", e);
                    case 2601:
                        throw new AuctionSiteNameAlreadyInUseException("SITE->Name already in USE");
                    default:
                        throw new AuctionSiteInvalidOperationException("DEFAULT EXC", e);
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            
            var userEntity = modelBuilder.Entity<User>();
            
            userEntity.HasOne(user => user.Session)
            .WithOne(session => session.Owner)
            .HasForeignKey<Session>(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

            userEntity.HasMany(user => user.SellingAuctions)
            .WithOne(auction => auction.SellerUser)
            .HasForeignKey(auction => auction.SellerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            userEntity.HasMany(user => user.WinningAuctions).WithOne(auction => auction.WinnerUser)
                .HasForeignKey(auction => auction.WinnerUserId).OnDelete(DeleteBehavior.NoAction);

            var sessionEntity = modelBuilder.Entity<Session>();

            sessionEntity.HasOne(session => session.Site)
            .WithMany(site => site.Sessions)
            .HasForeignKey(session => session.SiteId)
                .OnDelete(DeleteBehavior.NoAction);

            var auctionEntity = modelBuilder.Entity<Auction>();

            auctionEntity.HasOne(auction => auction.Site)
            .WithMany(site => site.Auctions)
            .HasForeignKey(auction => auction.SiteId)
                .OnDelete(DeleteBehavior.NoAction);
        }
        #endregion
    }
}
