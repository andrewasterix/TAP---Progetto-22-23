using Microsoft.Data.SqlClient;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using TAP22_23_AuctionSite.Data;

namespace TAP22_23_AuctionSite.Logic {
    internal class Host : IHost {

        private readonly string _connectionString;

        private readonly IAlarmClockFactory _alarmClockFactory;

        #region Costructor
        public Host(string connectionString, IAlarmClockFactory alarmClockFactory) {
            _connectionString = connectionString;
            _alarmClockFactory = alarmClockFactory;
        }
        #endregion

        #region IHost Properties
        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            using var conn = new AuctionDbContext(_connectionString);
            
            IQueryable<Site> sites;
            
            try {
                sites = conn.Sites.AsQueryable();
            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("SQL Exception", e);
            }
            
            foreach (var site in sites)
                yield return (site.Name, site.Timezone);
        }
        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds, double minimumBidIncrement) {
            #region Sanity Checks
            if (name == null)
                throw new AuctionSiteArgumentNullException("Site Name == NULL");

            if (name.Length < DomainConstraints.MinSiteName || name.Length > DomainConstraints.MaxSiteName)
                throw new AuctionSiteArgumentException($"Site Name.Length must be between {DomainConstraints.MinSiteName} and {DomainConstraints.MaxSiteName}");

            if (timezone < DomainConstraints.MinTimeZone || timezone > DomainConstraints.MaxTimeZone)
                throw new AuctionSiteArgumentOutOfRangeException("Timezone must be between -12 and 12");

            if (sessionExpirationTimeInSeconds <= 0)
                throw new AuctionSiteArgumentOutOfRangeException("SessionExpirationTime < 0");

            if (minimumBidIncrement <= 0)
                throw new AuctionSiteArgumentOutOfRangeException("MinimumBidIncrement > 0");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);

            var thisSites = conn.Sites.SingleOrDefault(s => s.Name == name);

            if (thisSites != null)
                throw new AuctionSiteNameAlreadyInUseException("AuctionSite Name already in use");

            conn.Sites.Add(new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement,
                _alarmClockFactory.InstantiateAlarmClock(timezone), _connectionString));
            conn.SaveChanges();
        }

        public ISite LoadSite(string name) {
            #region Sanity Checks
            if (name == null)
                throw new AuctionSiteArgumentNullException("SITE->Name  == NULL");

            if (name.Length < DomainConstraints.MinSiteName || name.Length > DomainConstraints.MaxSiteName)
                throw new AuctionSiteArgumentException($"SITE->Name.Length must be between {DomainConstraints.MinSiteName} and {DomainConstraints.MaxSiteName}");
            #endregion

            using var conn = new AuctionDbContext(_connectionString);
            try {
                var thisSite = conn.Sites.SingleOrDefault(s => s.Name == name);

                if (thisSite == null)
                    throw new AuctionSiteInexistentNameException(name, "SITE does NOT EXIST");

                var alarmClock = _alarmClockFactory.InstantiateAlarmClock(thisSite.Timezone);
                var alarm = alarmClock.InstantiateAlarm(5 * 60 * 1000);

                var newSite = new Site(thisSite.Name, thisSite.Timezone, thisSite.SessionExpirationInSeconds, thisSite.MinimumBidIncrement, _alarmClockFactory.InstantiateAlarmClock(thisSite.Timezone),
                    _connectionString) { SiteId = thisSite.SiteId };

                alarm.RingingEvent += newSite.RemoveSessions;

                return newSite;

            } catch (SqlException e) {
                throw new AuctionSiteUnavailableDbException("SQL Exception.", e);
            }
        }
        #endregion
    }
}
