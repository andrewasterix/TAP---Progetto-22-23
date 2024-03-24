namespace Test_TAP22_23_AuctionSite {
    /// <summary>
    /// Sanity check tests to verify the correct extension of TapDbContext by students' DbContextes
    /// </summary>
    [TestFixture]
    class DbContextTests : AuctionSiteTests {
        [Test]
        public void DbContext_Extends_TAPDbContext() {
            Assert.That(TapDbContext.TapDbContextIsUsed, Is.True);
        }

        [Test]
        public void OnConfiguringIsOk() {
            Assert.That(TapDbContext.OnConfiguringOk, "OnConfiguring override does not invoke base.OnConfiguring");
        }
    }
}