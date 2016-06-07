framework '4.6x86'

properties {
    $base_directory = Resolve-Path ..
    $publish_directory = "$base_directory\.build\publish"
    $build_output_root_directory = "$base_directory\.build"
    $src_directory = "$base_directory\src"
    $test_directory = "$base_directory\test"
    $samples_directory = "$base_directory\samples"
    $artifacts_directory = "$base_directory\.build\artifacts"
    $src_projects = gci $src_directory -Include *.csproj -Recurse
    $test_projects = gci $test_directory -Include *.csproj -Recurse
    $sample_projects = gci $samples_directory -Include *.csproj -Recurse
    $projects = $src_projects + $test_projects + $sample_projects  
    $target_config = "Release"

    $assemblyInfoFilePath = "$base_directory\CommonAssemblyInfo.cs"

    $ilMergeModule.ilMergePath = "$base_directory\bin\ilmerge-bin\ILMerge.exe"
    $nuget_dir = "$src_directory\.nuget"
    $sln_file = Join-Path $base_directory "Even.sln"

    if($build_number -eq $null) {
		$build_number = 0
	}

    if($runPersistenceTests -eq $null) {
    	$runPersistenceTests = $false
    }

	if($offline -eq $null) {
		$offline = $false
	}

    $isAppVeyorBuild = $env:APPVEYOR -eq "True"
}

FormatTaskName (("-"*25) + "[{0}]" + ("-"*30))

task default -depends Build

task Build -depends Clean, version, Compile, Test
task Clean -depends clean-directories, clean-source-projects, clean-test-projects, clean-sample-projects
task Test -depends RunUnitTests

task clean-source-projects {    
    foreach ($project in $src_projects) {
        $projectDir = $project.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = Join-Path $artifacts_directory $projectName 
        EnsureDirectory ($projectOutputs)
        exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /t:Clean }	                    
    }
}

task clean-test-projects {    
    foreach ($project in $test_projects) {
        $projectDir = $project.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = Join-Path $artifacts_directory $projectName
        EnsureDirectory ($projectOutputs)
        exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /t:Clean }	            
    }
}

task clean-sample-projects {
    foreach ($project in $sample_projects) {
        $projectDir = $project.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = Join-Path $artifacts_directory $projectName
        EnsureDirectory ($projectOutputs)
        exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /t:Clean }	            
    }
}

task compile-source-projects -depends clean-source-projects {
    foreach ($project in $src_projects) {
        $projectDir = $project.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = Join-Path $artifacts_directory $projectName
        $outDir = Join-path $projectOutputs "bin"
        if (-not ($outdir.EndsWith("\"))) { 
            $outdir += '\' #MSBuild requires OutDir end with a trailing slash #awesome 
        }

        if ($outdir.Contains(" ")) { 
            $outdir="""$($outdir)\""" #read comment from Johannes Rudolph here: http://www.markhneedham.com/blog/2008/08/14/msbuild-use-outputpath-instead-of-outdir/ 
        } 
	    exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /p:TargetFrameworkVersion=v4.5.1 /p:OutDir=$outdir }        
    }    
}

task compile-test-projects -depends clean-test-projects {
    foreach ($project in $test_projects) {        
	    exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /p:TargetFrameworkVersion=v4.5.1 }        
    }    
}

task compile-sample-projects -depends clean-sample-projects {
    foreach ($project in $sample_projects) {        
	    exec { msbuild /nologo /verbosity:quiet $project /p:Configuration=$target_config /p:TargetFrameworkVersion=v4.5.1 }        
    }    
}

task nuget-restore -depends acquire-buildtools {
    $nuget = Update-NuGet 
    $cmdArgs = @("restore")
    $cmdArgs += "$sln_file"
    exec {& $nuget $cmdArgs }
}

task Compile -depends nuget-restore, compile-source-projects, compile-test-projects, compile-sample-projects {    
}

task RunUnitTests -depends acquire-testingtools {
	"Unit Tests"
    $xunit_path = $script:xunit_path
    $hasError = $false
    foreach ($testProj in $test_projects) {
        $projectDir = $testProj.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = (Join-Path $artifacts_directory $projectName)
        $testResultsDirectory =  (Join-Path $projectOutputs "testResults")
        $testResultsFile = (Join-Path $testResultsDirectory ("{0}-testResults.xml" -f $projectName))        
        EnsureDirectory($testResultsDirectory)
        $test = ls ("{0}/bin/$target_config" -f $projectDir) -Include '*.Tests.dll' -Recurse | select -First 1
                
        $cmdArgs = @("$test")
        $cmdArgs += "-notrait"
        $cmdArgs += 'Category=Unstable'
        $cmdArgs += "-xml"
        $cmdArgs += '"{0}"' -f $testResultsFile        
        if($isAppVeyorBuild) {
            $cmdArgs += "-appveyor"
        }
        exec {& $xunit_path $cmdArgs}        
    }	           

    if ($hasError) {
        throw "RunUnitTests: Error encountered while executing xunit tests"
    }
}

task Package -depends Build, nuget-pack 

task clean-directories {
    Clean-Item $publish_directory -ea SilentlyContinue
    Clean-Item $artifacts_directory -ea SilentlyContinue
    Clean-Item $build_output_root_directory
    EnsureDirectory $build_output_root_directory
}

task nuget-pack -depends version {
    $packageVersion = ($script:VersionInfo).PackageVersion
    Write-Host "Using PackageVersion: $packageVersion"
    $nuget = Get-NuGet 

    foreach ($project in $src_projects) {
        $projectDir = $project.DirectoryName
        $projectName = Split-Path $projectDir -Leaf
        $projectOutputs = (Join-Path $artifacts_directory $projectName)
        $nuspecPath = (Join-Path $projectDir ("{0}.nuspec" -f $projectName))        
        $nugetOutput = (Join-Path $projectOutputs "nuget")

        EnsureDirectory($projectOutputs)
        EnsureDirectory($nugetOutput)
        copy $nuspecPath $projectOutputs -Force
        $nugetArgs = @('pack')
        $nugetArgs += ('"{0}"' -f $nuspecPath)
        $nugetArgs += "-basepath"
        $nugetArgs += $projectOutputs
        $nugetArgs += "-o"
        $nugetArgs += $nugetOutput
        $nugetArgs += "-version"
        $nugetArgs += "$packageVersion"
        exec {& $nuget $nugetArgs }
        #gci -r -i *.nuspec "$nuget" |% { .$nuget_dir\nuget.exe pack $_ -basepath $base_directory -o $publish_directory -version $packageVersion }
    }	
}

task acquire-testingtools {
    Write-Host  "Acquiring XUnit..."
    $script:xunit_path = Get-XUnitRunnerPath
    Set-XUnitPath $script:xunit_path 
} 

task acquire-buildtools -precondition {-not $offline} {
    Write-Host  "Acquiring GitVersion.CommandLine..."
    $script:GitVersionExe = Get-GitVersionCommandline    
}

task version -depends acquire-buildtools {    
    $script:versionInfo = Invoke-GitVersion -UpdateAssemblyInfo -AssemblyInfoPaths $assemblyInfoFilePath -GitVersionExePath $script:GitVersionExe
    Write-Host ('VersionInfo: {0}' -f $script:versionInfo)    
}

function EnsureDirectory {
	param($directory)

	if(!(test-path $directory))
	{
		mkdir $directory
	}
}
