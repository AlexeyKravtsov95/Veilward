using System;
using NUnit.Framework;
using Veilward.Editor;

namespace Veilward.Tests.EditMode
{
    public sealed class IosBuildTests
    {
        [Test]
        public void SavedProjectHasProductionBuildContract() => IosBuild.ValidateProject();

        [TestCase("0.1.0", "42")]
        [TestCase("9999.99.99", "9999")]
        public void AcceptsExplicitVersion(string version, string number) => IosBuild.ValidateVersion(version, number);

        [TestCase(null, "42")]
        [TestCase("1.0", "42")]
        [TestCase("1.0.0\n", "42")]
        [TestCase("1.0.0", "0")]
        [TestCase("1.0.0", "01")]
        [TestCase("1.0.0", "10000")]
        [TestCase("1.0.0", null)]
        public void RejectsInvalidVersion(string version, string number) =>
            Assert.Throws<ArgumentException>(() => IosBuild.ValidateVersion(version, number));
    }
}
