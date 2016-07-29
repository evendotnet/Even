
///////////////////////////////////////////////////////////////////////////////
// Simple Cake Build using MSBuild
///////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////
// Install Addins
///////////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////
// Install Tools
///////////////////////////////////////////////////////////////////////////////
#tool "nuget:?package=xunit.runner.console&version=2.1.0"
#tool "nuget:?package=GitVersion.CommandLine"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var runAllTests = Argument<bool>("runAllTests", false);
var skipNuGetRestore = Argument<bool>("skipNuGetRestore", false);
var skipTests = Argument<bool>("skiptests", false);
// Defaulting to the x86 version of the xunit runner
var xunitRunnerPath = Argument<string>("xunitRunnerPath", "./tools/xunit.runner.console/tools/xunit.console.x86.exe");
var nugetPublishSource = Argument("nugetPublishSource", "https://api.nuget.org/v3/index.json");
var nugetApiKey = Argument("nugetApiKey","");
//////////////////////////////////////////////////////////////////////
// CUSTOMIZE PER Project 
//////////////////////////////////////////////////////////////////////
var traitsToExclude = new Dictionary<string, IList<string>>{
    {"Category", new []{"Integration","Debug","Unstable"}}
};
//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var rootDir = Directory("./");
var srcDir = rootDir + Directory("src");

var srcProjects = GetFiles("./src/**/*.csproj");
var testProjects = GetFiles("./test/**/*.csproj");
var allProjects = GetFiles("./**/*.csproj");

var rootOutDir = rootDir + Directory(".build");

var solutionFile = rootDir + File("Even.sln");
// Define nugetPushSource

var projects = new List<ProjectInfo>();
GitVersion version;
//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup(context=>{
    version = GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = false,
            OutputType = GitVersionOutput.BuildServer
    });

    foreach(var project in allProjects){
        var projectInfo = new ProjectInfo(Context, project);
        projectInfo.OutputDirectory = rootOutDir + Directory(projectInfo.DirectoryName);
        projectInfo.BuildOutputDirectory = Directory(project.GetDirectory().FullPath) + Directory("bin") + Directory(configuration); 
        projects.Add(projectInfo);
    }

    foreach(var testProject in testProjects){
        var testProjectPath = testProject.FullPath;
        var project = projects.FirstOrDefault(p=> String.Equals(p.Path.FullPath, testProjectPath, StringComparison.OrdinalIgnoreCase));
        if(project != null){
            Information("{0} is a test project.", project.Path);
            project.MarkAsTestProject();
        }
    }
});

Task("Print-Info").Does(()=>{

    Information("###############################################");
    Information("# VersionInfo:");
    Information("# NuGetVersion: {0}", version.NuGetVersion);
    Information("# SemVer: {0}", version.SemVer);
    Information("# InformationalVersion: {0}", version.InformationalVersion);
    Information("###############################################");
});

Task("Update-Versions").Does(()=>{
    version = GitVersion(new GitVersionSettings {
            UpdateAssemblyInfo = true,
            UpdateAssemblyInfoFilePath = File(".\\CommonAssemblyInfo.cs")            
    });
});

Task("Clean")
    .Does(() =>
{
    foreach(var project in projects){        
        CleanDirectory(project.OutputDirectory);
        //CleanDirectory(project.TestResultsOutputDirectory);
        if(FileExists(project.NuSpec)) {
            CleanDirectory(project.OutputDirectory.Combine(Directory("nuget")));
        }
        if(project.IsTestProject){
            var testResultsDir = project.OutputDirectory.Combine(Directory("testResults"));
            CleanDirectory(testResultsDir);
        }        
    }
});

Task("Clean-BuildOutput").Does(() =>
{
    foreach(var project in projects){
        CleanDirectory(project.BuildOutputDirectory);
    }
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .WithCriteria(() => !skipNuGetRestore)
    .Does(() =>
{
    NuGetRestore(solutionFile);
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild(solutionFile, settings =>
        settings.SetConfiguration(configuration));
    }
    else
    {
      // Use XBuild
      XBuild(solutionFile, settings =>
        settings.SetConfiguration(configuration));
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .WithCriteria(() => !skipTests)
    .Does(() =>
{
    foreach(var project in projects.Where(p=>p.IsTestProject)) {
        var testAssembly = project.BuildOutputDirectory.CombineWithFilePath(File(project.Name+".dll"));
        if(FileExists(testAssembly)){
            var testResultsDir = project.OutputDirectory.Combine(Directory("testResults"));
            EnsureDirectoryExists(testResultsDir);

            var settings = new XUnit2Settings{
                HtmlReport=true,
                XmlReport=true,
                OutputDirectory = testResultsDir
            };

            if(!string.IsNullOrEmpty(xunitRunnerPath)){
                settings.ToolPath = xunitRunnerPath;
            }

            if(!runAllTests) {
                foreach (var kvp in traitsToExclude) {
                    foreach(var value in kvp.Value) {
                        settings.ExcludeTrait(kvp.Key, value);
                    }
                }
            }
            XUnit2(testAssembly.FullPath, settings);
        }
    }
    
});

Task("Copy-BuildOutput")
    .Does(()=>{
        foreach(var project in projects) {
            CopyDirectory(project.BuildOutputDirectory, project.OutputDirectory.Combine(Directory("bin")));
        }
    });

Task("NuGet-Pack")
    //.IsDependentOn("Copy-BuildOutput")
    .Does(()=>{
        foreach(var project in projects){
            if(FileExists(project.NuSpec)){
                var nugetFolder = project.OutputDirectory.Combine(Directory("nuget"));
                //EnsureDirectoryExists(nugetFolder);
                CopyFileToDirectory(project.NuSpec, project.OutputDirectory);
                Information("Working Directory is: {0}", project.OutputDirectory);
                var packSettings = new NuGetPackSettings {
                    OutputDirectory = nugetFolder,
                    Version = version.NuGetVersion,
                    WorkingDirectory = project.OutputDirectory,
                    Properties = new Dictionary<string,string>{
                        {"Configuration", configuration}
                    }
                };
                NuGetPack(project.NuSpec, packSettings);
            }
        }
    });

Task("NuGet-Push")
    .Does(()=>{
        var packages = GetFiles("./.build/**/nuget/*.nupkg");
        NuGetPush(packages, new NuGetPushSettings{
            Source = nugetPublishSource,
            ApiKey = nugetApiKey
        });
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("About")
    .IsDependentOn("Print-Info");

Task("DeepClean")
    .IsDependentOn("Clean-BuildOutput")
    .IsDependentOn("Clean");


Task("Package")        
    .IsDependentOn("NuGet-Pack");

Task("Publish")    
    .IsDependentOn("NuGet-Push");

Task("BuildAndPackage")    
    .IsDependentOn("Update-Versions")
    .IsDependentOn("Print-Info")
    .IsDependentOn("DeepClean")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Package");

Task("BuildAndPublish")
    .IsDependentOn("BuildAndPackage")
    .IsDependentOn("Publish");

Task("CI")
    .IsDependentOn("BuildAndPublish");

Task("BLD")
    .IsDependentOn("Clean")
    .IsDependentOn("Run-Unit-Tests")
    .IsDependentOn("Package");

Task("Default")
    .IsDependentOn("BuildAndPackage");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);

//////////////////////////////////////////////////////////////////////
// Utility and Helper
//////////////////////////////////////////////////////////////////////
public class ProjectInfo {
    public ProjectInfo(ICakeContext context, FilePath path) {
        Path = path;
        Name = path.GetFilenameWithoutExtension().ToString();
        Directory = path.GetDirectory();
        DirectoryName = Directory.GetDirectoryName();
        NuSpec = Directory.CombineWithFilePath(path.ChangeExtension(".nuspec").GetFilename());
        Configuration = context.Argument("configuration", "Release");        
    }
    public FilePath Path {get; private set;}
    public DirectoryPath Directory {get; private set;}
    public string Name {get; private set;}
    public string DirectoryName {get; private set;}
    public FilePath NuSpec {get; set;}
    public DirectoryPath OutputDirectory {get;set;}     
    public DirectoryPath BuildOutputDirectory {get;set;}
    public bool IsTestProject {get; private set;}
    public string Configuration {get; private set;} 
    public void MarkAsTestProject() {
        IsTestProject = true;

    }    
}
