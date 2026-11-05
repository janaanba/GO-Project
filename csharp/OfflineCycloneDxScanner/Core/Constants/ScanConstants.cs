namespace OfflineCycloneDxScanner;

public static class ScanConstants
{
	public const string CycloneDxFormat = "cyclonedx";

	public const string CycloneDxBomFormat = "CycloneDX";

	public const string CycloneDxSpecVersion = "1.7";

	public const string ComponentTypeLibrary = "library";

	public const string ComponentTypeApplication = "application";

	public const string ToolVendor = "aquasecurity";

	public const string ToolName = "trivy";

	public const string ToolManufacturer = "Aqua Security Software Ltd.";

	public const string ToolVersion = "1.0.0";

	public const string ConanLockFile = "conan.lock";

	public const string DartLockFile = "pubspec.lock";

	public const string ComposerLockFile = "composer.lock";

	public const string GradleLockFile = "gradle.lockfile";

	public const string MavenProjectFile = "pom.xml";

	public const string PackagesSection = "packages";

	public const string DevelopmentPackagesSection = "packages-dev";

	public const string GraphLockSection = "graph_lock";

	public const string NodesSection = "nodes";

	public const string RequiresSection = "requires";

	public const string ComposerRequirementsSection = "require";

	public const string TestConfigurationPrefix = "test";

	public const string PhpRequirement = "php";

	public const string ExtensionRequirementPrefix = "ext-";

	public const string DirectDevelopmentRelationship = "direct dev";

	public const string DirectRelationship = "direct";

	public const string IndirectRelationship = "indirect";

	public const string PurlComposer = "composer";

	public const string PurlConan = "conan";

	public const string PurlDart = "pub";

	public const string PurlMaven = "maven";
}
