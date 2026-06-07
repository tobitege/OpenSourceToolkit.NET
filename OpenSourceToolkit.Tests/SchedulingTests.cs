using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSourceToolkit.Scheduling;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenSourceToolkit.Tests
{
    [TestClass]
    public class SchedulingTests
    {
        [TestMethod]
        public void CronScheduler_Parse_ValidExpression()
        {
            bool executed = false;
            // Run every minute
            var scheduler = new CronScheduler("* * * * *", () => { executed = true; });
            Assert.IsNotNull(scheduler);
            Assert.IsFalse(executed);

            // We can't easily wait for a minute in a unit test,
            // but we verified the constructor parsed the schedule successfully.
        }

        [TestMethod]
        public void CronScheduler_Parse_InvalidExpression_Throws()
        {
            Assert.Throws<NCrontab.CrontabException>(() => new CronScheduler("invalid cron", () => { }));
        }

        [TestMethod]
        public void CronScheduler_GetNextOccurrences_ReturnsIncreasingSequence()
        {
            var occurrences = CronScheduler.GetNextOccurrences("*/5 * * * *", 3).ToList();

            Assert.AreEqual(3, occurrences.Count);
            Assert.IsTrue(occurrences[1] > occurrences[0]);
            Assert.IsTrue(occurrences[2] > occurrences[1]);
            Assert.AreEqual(TimeSpan.FromMinutes(5), occurrences[1] - occurrences[0]);
            Assert.AreEqual(TimeSpan.FromMinutes(5), occurrences[2] - occurrences[1]);
        }

        [TestMethod]
        public void CronScheduler_GetNextOccurrences_InvalidExpression_Throws()
        {
            Assert.Throws<NCrontab.CrontabException>(() => CronScheduler.GetNextOccurrences("invalid cron", 1));
        }
    }
}
