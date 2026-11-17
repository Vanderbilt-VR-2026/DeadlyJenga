using NUnit.Framework;

namespace DeadlyJenga.Tests
{
    // TEMPORARY: proves the pipeline reports failures. Remove in the next commit.
    public class DeliberateFailureTest
    {
        [Test]
        public void PipelineReportsThisFailure()
        {
            Assert.AreEqual(2, 1 + 2, "Deliberate failure to check the test bridge.");
        }
    }
}
