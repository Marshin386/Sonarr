using System.Linq;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Common.Test.EnvironmentInfo
{
    [TestFixture]
    public class BuildInfoFixture
    {
        [Test]
        public void should_return_version()
        {
            BuildInfo.Version.Major.Should().BeOneOf(5, 10);
        }

        [Test]
        public void should_get_branch()
        {
            BuildInfo.Branch.Should().NotBe("unknown");
            BuildInfo.Branch.Should().NotBeNullOrWhiteSpace();
        }

        [Test]
        public void should_prefer_informational_version_for_display()
        {
            var informational = typeof(BuildInfo).Assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion;

            if (string.IsNullOrWhiteSpace(informational))
            {
                BuildInfo.VersionString.Should().Be(BuildInfo.Version.ToString());
                return;
            }

            var plus = informational.IndexOf('+');
            var expected = plus >= 0 ? informational.Substring(0, plus) : informational;
            BuildInfo.VersionString.Should().Be(expected);
        }
    }
}
