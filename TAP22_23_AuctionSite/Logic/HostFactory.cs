using Microsoft.Data.SqlClient;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using TAP22_23_AuctionSite.Data;

namespace TAP22_23_AuctionSite.Logic {
    internal class HostFactory : IHostFactory {
        #region IHostFactory Properties
        public void CreateHost(string connectionString) {
            if (connectionString == null)
                throw new AuctionSiteArgumentNullException("Connection String == NULL.");
            try
            {
                using var conn = new AuctionDbContext(connectionString);
                conn.Database.EnsureDeleted();
                conn.Database.EnsureCreated();
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("DB unavailable exception", e);
            }
        }

        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory) {
            if (connectionString == null)
                throw new AuctionSiteArgumentNullException("Connection String == NULL");

            if (alarmClockFactory == null)
                throw new AuctionSiteArgumentNullException("AlarmClockFactory == NULL");

            try
            {
                using var conn = new AuctionDbContext(connectionString);
                conn.Database.EnsureCreated();
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("DB unavailable exception", e);
            }

            return new Host(connectionString, alarmClockFactory);
        }
        #endregion
    }
}
