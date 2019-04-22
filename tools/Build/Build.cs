using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Faithlife.Build;
using static Faithlife.Build.AppRunner;
using static Faithlife.Build.BuildUtility;
using static Faithlife.Build.DotNetRunner;

internal static class Build
{
	public static int Main(string[] args) => BuildRunner.Execute(args, build =>
	{
		var codegen = "fsdgenswagger";

		var dotNetTools = new DotNetTools(Path.Combine("tools", "bin")).AddSource(Path.Combine("tools", "bin"));

		var dotNetBuildSettings = new DotNetBuildSettings
		{
			DocsSettings = new DotNetDocsSettings
			{
				GitLogin = new GitLoginInfo("FacilityApiBot", Environment.GetEnvironmentVariable("BUILD_BOT_PASSWORD") ?? ""),
				GitAuthor = new GitAuthorInfo("FacilityApiBot", "facilityapi@gmail.com"),
				SourceCodeUrl = "https://github.com/FacilityApi/RepoName/tree/master/src",
			},
			DotNetTools = dotNetTools,
			ProjectUsesSourceLink = name => !name.StartsWith("fsdgen", StringComparison.Ordinal),
		};

		build.AddDotNetTargets(dotNetBuildSettings);

		build.Target("codegen")
			.DependsOn("build")
			.Does(() => codeGen(verify: false));

		build.Target("verify-codegen")
			.DependsOn("build")
			.Does(() => codeGen(verify: true));

		build.Target("test")
			.DependsOn("verify-codegen");

		build.Target("default")
			.DependsOn("build");

		void codeGen(bool verify)
		{
			string configuration = dotNetBuildSettings.BuildOptions.ConfigurationOption.Value;
			string versionSuffix = $"cg{DateTime.UtcNow:yyyyMMddHHmmss}";
			RunDotNet("pack", Path.Combine("src", codegen, $"{codegen}.csproj"), "-c", configuration, "--no-build",
				"--output", Path.GetFullPath(Path.Combine("tools", "bin")), "--version-suffix", versionSuffix);

			string packagePath = FindFiles($"tools/bin/{codegen}.*-{versionSuffix}.nupkg").Single();
			string packageVersion = Regex.Match(packagePath, @"[/\\][^/\\]*\.([0-9]+\.[0-9]+\.[0-9]+(-.+)?)\.nupkg$").Groups[1].Value;
			string toolPath = dotNetTools.GetToolPath($"{codegen}/{packageVersion}");

			string verifyOption = verify ? "--verify" : null;

			RunApp(toolPath, "example/ExampleApi.fsd", "example/output/swagger", "--json", verifyOption);
			RunApp(toolPath, "example/ExampleApi.fsd", "example/output/swagger", verifyOption);
			RunApp(toolPath, "example/output/swagger/ExampleApi.json", "example/output/swagger/fsd", "--fsd", verifyOption);
			if (verify)
				RunApp(toolPath, "example/output/swagger/ExampleApi.yaml", "example/output/swagger/fsd", "--fsd", verifyOption);

			foreach (var yamlPath in FindFiles("example/*.yaml"))
				RunApp(toolPath, yamlPath, "example/output/fsd", "--fsd", verifyOption);

			Directory.CreateDirectory("example/output/fsd/swagger");
			foreach (var fsdPath in FindFiles("example/output/fsd/*.fsd"))
				RunApp(toolPath, fsdPath, $"example/output/fsd/swagger/{Path.GetFileNameWithoutExtension(fsdPath)}.yaml", verifyOption);
		}
	});
}