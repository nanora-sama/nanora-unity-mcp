using System.IO;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.Tests
{
    [TestFixture]
    public class SkillInstallerTests
    {
        private string m_tempRoot;
        private string m_sourceDir;
        private string m_targetDir;
        private string m_manifestPath;

        [SetUp]
        public void SetUp()
        {
            m_tempRoot = Path.Combine(Path.GetTempPath(), "nanora-skill-tests-" + System.Guid.NewGuid().ToString("N"));
            m_sourceDir = Path.Combine(m_tempRoot, "source");
            m_targetDir = Path.Combine(m_tempRoot, "target");
            m_manifestPath = Path.Combine(m_tempRoot, "manifest.json");
            Directory.CreateDirectory(m_sourceDir);
            Directory.CreateDirectory(m_targetDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_tempRoot))
            {
                Directory.Delete(m_tempRoot, recursive: true);
            }
        }

        private void CreateBundledSkill(string name, string content)
        {
            var dir = Path.Combine(m_sourceDir, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
        }

        [Test]
        public void GetBundledSkillNamesIn_ReturnsOnlyDirsWithSkillMd()
        {
            CreateBundledSkill("alpha", "# alpha");
            CreateBundledSkill("beta", "# beta");
            Directory.CreateDirectory(Path.Combine(m_sourceDir, "not-a-skill"));

            var names = SkillInstaller.GetBundledSkillNamesIn(m_sourceDir);

            Assert.AreEqual(2, names.Length);
            CollectionAssert.AreEqual(new[] { "alpha", "beta" }, names);
        }

        [Test]
        public void GetBundledSkillNamesIn_EmptyDirReturnsEmpty()
        {
            var names = SkillInstaller.GetBundledSkillNamesIn(m_sourceDir);
            Assert.AreEqual(0, names.Length);
        }

        [Test]
        public void GetBundledSkillNamesIn_MissingDirReturnsEmpty()
        {
            var names = SkillInstaller.GetBundledSkillNamesIn(Path.Combine(m_tempRoot, "does-not-exist"));
            Assert.AreEqual(0, names.Length);
        }

        [Test]
        public void InstallAllAt_InstallsFreshSkills()
        {
            CreateBundledSkill("skill-a", "# A");
            CreateBundledSkill("skill-b", "# B");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            Assert.AreEqual(2, reports.Length);
            Assert.IsTrue(reports.All(r => r.Result == SkillInstallResult.Installed));
            Assert.IsTrue(File.Exists(Path.Combine(m_targetDir, "skill-a", "SKILL.md")));
            Assert.IsTrue(File.Exists(Path.Combine(m_targetDir, "skill-b", "SKILL.md")));
            Assert.IsTrue(File.Exists(m_manifestPath));
        }

        [Test]
        public void InstallAllAt_SecondRunReportsUnchanged()
        {
            CreateBundledSkill("skill-a", "# A");

            SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);
            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            Assert.AreEqual(1, reports.Length);
            Assert.AreEqual(SkillInstallResult.Unchanged, reports[0].Result);
        }

        [Test]
        public void InstallAllAt_DryRunDoesNotWriteFiles()
        {
            CreateBundledSkill("skill-a", "# A");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: true, force: false);

            Assert.AreEqual(SkillInstallResult.Installed, reports[0].Result);
            Assert.IsFalse(File.Exists(Path.Combine(m_targetDir, "skill-a", "SKILL.md")));
            Assert.IsFalse(File.Exists(m_manifestPath));
        }

        [Test]
        public void InstallAllAt_UpdatedSourceTriggersUpdate()
        {
            CreateBundledSkill("skill-a", "# A v1");
            SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            File.WriteAllText(Path.Combine(m_sourceDir, "skill-a", "SKILL.md"), "# A v2");
            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            Assert.AreEqual(SkillInstallResult.Updated, reports[0].Result);
            Assert.AreEqual("# A v2", File.ReadAllText(Path.Combine(m_targetDir, "skill-a", "SKILL.md")));
        }

        [Test]
        public void InstallAllAt_UserModifiedSkipsWithoutForce()
        {
            CreateBundledSkill("skill-a", "# A v1");
            SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            // User modifies target
            var targetFile = Path.Combine(m_targetDir, "skill-a", "SKILL.md");
            File.WriteAllText(targetFile, "# User's custom edit");

            // Source also changes
            File.WriteAllText(Path.Combine(m_sourceDir, "skill-a", "SKILL.md"), "# A v2");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            Assert.AreEqual(SkillInstallResult.SkippedUserModified, reports[0].Result);
            Assert.AreEqual("# User's custom edit", File.ReadAllText(targetFile));
        }

        [Test]
        public void InstallAllAt_PreExistingUserFileIsProtected()
        {
            // User had their own skill before nanora ever installed.
            var userDir = Path.Combine(m_targetDir, "unity-material-repair");
            Directory.CreateDirectory(userDir);
            var userFile = Path.Combine(userDir, "SKILL.md");
            File.WriteAllText(userFile, "# My custom material repair");

            // Now nanora ships its version with the same name.
            CreateBundledSkill("unity-material-repair", "# Nanora's version");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            Assert.AreEqual(SkillInstallResult.SkippedUserModified, reports[0].Result);
            Assert.AreEqual("# My custom material repair", File.ReadAllText(userFile));
        }

        [Test]
        public void InstallAllAt_PreExistingUserFileCanBeForceReplaced()
        {
            var userDir = Path.Combine(m_targetDir, "unity-material-repair");
            Directory.CreateDirectory(userDir);
            var userFile = Path.Combine(userDir, "SKILL.md");
            File.WriteAllText(userFile, "# My custom material repair");

            CreateBundledSkill("unity-material-repair", "# Nanora's version");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: true);

            Assert.AreEqual(SkillInstallResult.Updated, reports[0].Result);
            Assert.AreEqual("# Nanora's version", File.ReadAllText(userFile));
            Assert.IsTrue(File.Exists(userFile + ".bak"));
        }

        [Test]
        public void InstallAllAt_ForceOverwritesUserModifiedAndCreatesBackup()
        {
            CreateBundledSkill("skill-a", "# A v1");
            SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            var targetFile = Path.Combine(m_targetDir, "skill-a", "SKILL.md");
            File.WriteAllText(targetFile, "# User's custom edit");
            File.WriteAllText(Path.Combine(m_sourceDir, "skill-a", "SKILL.md"), "# A v2");

            var reports = SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: true);

            Assert.AreEqual(SkillInstallResult.Updated, reports[0].Result);
            Assert.AreEqual("# A v2", File.ReadAllText(targetFile));
            Assert.IsTrue(File.Exists(targetFile + ".bak"));
            Assert.AreEqual("# User's custom edit", File.ReadAllText(targetFile + ".bak"));
        }

        [Test]
        public void ComputeFileHash_SameContentSameHash()
        {
            var a = Path.Combine(m_tempRoot, "a.txt");
            var b = Path.Combine(m_tempRoot, "b.txt");
            File.WriteAllText(a, "hello");
            File.WriteAllText(b, "hello");

            Assert.AreEqual(SkillInstaller.ComputeFileHash(a), SkillInstaller.ComputeFileHash(b));
        }

        [Test]
        public void ComputeFileHash_DifferentContentDifferentHash()
        {
            var a = Path.Combine(m_tempRoot, "a.txt");
            var b = Path.Combine(m_tempRoot, "b.txt");
            File.WriteAllText(a, "hello");
            File.WriteAllText(b, "world");

            Assert.AreNotEqual(SkillInstaller.ComputeFileHash(a), SkillInstaller.ComputeFileHash(b));
        }

        [Test]
        public void ComputeFileHash_MissingFileReturnsNull()
        {
            var missing = Path.Combine(m_tempRoot, "missing.txt");
            Assert.IsNull(SkillInstaller.ComputeFileHash(missing));
        }

        [Test]
        public void Manifest_RoundTripsSkillMetadata()
        {
            CreateBundledSkill("skill-a", "# A");
            SkillInstaller.InstallAllAt(m_sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);

            var manifest = SkillInstaller.LoadManifestFrom(m_manifestPath);
            Assert.IsNotNull(manifest["skills"]);
            Assert.IsNotNull(manifest["skills"]["skill-a"]);
            Assert.IsNotNull(manifest["skills"]["skill-a"]["installedHash"]);
            Assert.IsNotNull(manifest["skills"]["skill-a"]["installedAt"]);
        }

        [Test]
        public void BundledSkills_InstallCopiesActualPackageFiles()
        {
            var sourceDir = SkillInstaller.GetBundledSkillsDir();
            var reports = SkillInstaller.InstallAllAt(sourceDir, m_targetDir, m_manifestPath, dryRun: false, force: false);
            Assert.GreaterOrEqual(reports.Length, 5, "The actual installed package must supply its authored skills.");
            foreach (var report in reports)
            {
                Assert.AreEqual(SkillInstallResult.Installed, report.Result, report.Message);
                var source = Path.Combine(sourceDir, report.Name, "SKILL.md");
                var target = Path.Combine(m_targetDir, report.Name, "SKILL.md");
                Assert.IsTrue(File.Exists(target), target);
                Assert.AreEqual(File.ReadAllText(source), File.ReadAllText(target), report.Name);
            }
        }

        [Test]
        public void BundledSkillsDir_ContainsAuthoredRecipeSkills()
        {
            // Sanity check: make sure the 5 authored skills exist in the repo.
            var names = SkillInstaller.GetBundledSkillNames();
            CollectionAssert.Contains(names, "unity-material-repair");
            CollectionAssert.Contains(names, "unity-pre-pr-check");
            CollectionAssert.Contains(names, "unity-scene-setup");
            CollectionAssert.Contains(names, "unity-e2e-test");
            CollectionAssert.Contains(names, "unity-performance-budget");
        }
    }
}
