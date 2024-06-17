open System.Threading.Tasks
open Fake.Core
open Fake.IO
open Farmer
open Helpers
open System
open System.IO
open System.Linq
open System.IO.Compression
open System.Xml
open System.Text
open ICSharpCode.SharpZipLib.GZip
open ICSharpCode.SharpZipLib.Tar
open Octokit

initializeContext()

let sharedPath = Path.getFullName "src/Shared"
let serverPath = Path.getFullName "src/Server"
let clientPath = Path.getFullName "src/Client"
let deployPath = Path.getFullName "deploy"
let sharedTestsPath = Path.getFullName "tests/Shared"
let serverTestsPath = Path.getFullName "tests/Server"
let clientTestsPath = Path.getFullName "tests/Client"

let toolVersion() =
    let projectFilePath = Path.Combine(serverPath, "PulumiImporter.fsproj")
    let content = File.ReadAllText projectFilePath
    let doc = XmlDocument()
    use content = new MemoryStream(Encoding.UTF8.GetBytes content)
    doc.Load(content)
    doc.GetElementsByTagName("Version").[0].InnerText

let artifacts = "./artifacts"

let createTarGz (source: string) (target: string)  =
    let outStream = File.Create target
    let gzipOutput = new GZipOutputStream(outStream)
    let tarArchive = TarArchive.CreateOutputTarArchive(gzipOutput);
    for file in Directory.GetFiles source do
        let tarEntry = TarEntry.CreateEntryFromFile file
        tarEntry.Name <- Path.GetFileName file
        tarArchive.WriteEntry(tarEntry, false)

    for directory in Directory.GetDirectories source do
        for file in Directory.GetFiles directory do
            let tarEntry = TarEntry.CreateEntryFromFile file
            tarEntry.Name <- Path.GetFileName file
            tarArchive.WriteEntry(tarEntry, false)

    tarArchive.Close()

let cleanArtifacts() = Shell.deleteDirs [artifacts]

Target.create "CleanArtifacts" (fun _ -> cleanArtifacts())

let createArtifacts() =
    let version = toolVersion()
    let cwd = serverPath
    let runtimes = [
        "linux-x64"
        "linux-arm64"
        "osx-x64"
        "osx-arm64"
        "win-x64"
        "win-arm64"
    ]

    Shell.deleteDirs [
        Path.Combine(cwd, "bin")
        Path.Combine(cwd, "obj")
        artifacts
    ]

    let binary = "pulumi-tool-importer"
    for runtime in runtimes do
        printfn $"Building binary {binary} for {runtime}"
        let args = [
            "publish"
            "--configuration Release"
            $"--runtime {runtime}"
            "--self-contained true"
            "-p:PublishSingleFile=true"
            "/p:DebugType=None"
            "/p:DebugSymbols=false"
        ]
        let exitCode = Shell.Exec("dotnet", String.concat " " args, cwd)
        if exitCode <> 0 then
            failwith $"failed to build for runtime {runtime}"

    Directory.create artifacts
    for runtime in runtimes do
        let publishPath = Path.Combine(cwd, "bin", "Release", "net6.0", runtime, "publish")
        let destinationRuntime =
            match runtime with
            | "osx-x64" -> "darwin-amd64"
            | "osx-arm64" -> "darwin-arm64"
            | "linux-x64" -> "linux-amd64"
            | "linux-arm64" -> "linux-arm64"
            | "win-x64" -> "windows-amd64"
            | "win-arm64" -> "windows-arm64"
            | _ -> runtime

        Shell.copyDir publishPath deployPath  (fun _ -> true)
        let destination = Path.Combine(artifacts, $"{binary}-v{version}-{destinationRuntime}.tar.gz")
        createTarGz publishPath destination

let inline await (task: Task<'t>) =
    task
    |> Async.AwaitTask
    |> Async.RunSynchronously

let releaseVersion (release: Release) =
    if not (String.IsNullOrWhiteSpace(release.Name)) then
        release.Name.Substring(1, release.Name.Length - 1)
    elif not (String.IsNullOrWhiteSpace(release.TagName)) then
        release.TagName.Substring(1, release.TagName.Length - 1)
    else
        ""

let createAndPublishArtifacts() =
    let version = toolVersion()
    let github = GitHubClient(ProductHeaderValue "PulumiBicepConverter")
    let githubToken = Environment.GetEnvironmentVariable "GITHUB_TOKEN"
    // only assign github token to the client when it is available (usually in Github CI)
    if not (isNull githubToken) then
        printfn "GITHUB_TOKEN is available"
        github.Credentials <- Credentials(githubToken)
    else
        printfn "GITHUB_TOKEN is not set"

    let githubUsername = "Zaid-Ajaj"
    let githubRepo = "pulumi-tool-importer"
    let releases = await (github.Repository.Release.GetAll(githubUsername, githubRepo))
    let alreadyReleased = releases |> Seq.exists (fun release -> releaseVersion release = version)

    if alreadyReleased then
        printfn $"Release v{version} already exists, skipping publish"
    else
        printfn $"Preparing artifacts to release v{version}"
        createArtifacts()
        let releaseInfo = NewRelease($"v{version}")
        let release = await (github.Repository.Release.Create(githubUsername, githubRepo, releaseInfo))
        for file in Directory.EnumerateFiles artifacts do
            let asset = ReleaseAssetUpload()
            asset.FileName <- Path.GetFileName file
            asset.ContentType <- "application/tar"
            asset.RawData <- File.OpenRead(file)
            let uploadedAsset = await (github.Repository.Release.UploadAsset(release, asset))
            printfn $"Uploaded {uploadedAsset.Name} into assets of v{version}"

Target.create "CreateArtifacts" (fun _ -> createArtifacts())

Target.create "CreateAndPublishArtifacts" (fun _ ->
    createAndPublishArtifacts()
)

Target.create "Clean" (fun _ ->
    Shell.cleanDir deployPath
    run dotnet "fable clean --yes" clientPath // Delete *.fs.js files created by Fable
)

Target.create "InstallClient" (fun _ ->
    run dotnet "tool restore" "."
    run npm "install" "."
)

Target.create "BuildClient" (fun _ ->
    run dotnet "fable -o output -s --run npm run build" clientPath
)

Target.create "LocalNugetBundle" (fun _ ->
    let outputPath = deployPath
    let nugetPackage = Directory.EnumerateFiles(outputPath, "PulumiSchemaExplorer.*.nupkg", SearchOption.AllDirectories).First()
    printfn "Installing %s locally" nugetPackage
    let nugetParent = DirectoryInfo(nugetPackage).Parent.FullName
    let nugetFileName = Path.GetFileNameWithoutExtension(nugetPackage)
    // Unzip the nuget
    ZipFile.ExtractToDirectory(nugetPackage, Path.Combine(nugetParent, nugetFileName))
    // delete the initial nuget package
    File.Delete nugetPackage
    let serverDll = Directory.EnumerateFiles(outputPath, "PulumiSchemaExplorer.dll", SearchOption.AllDirectories).First()
    let serverDllParent = DirectoryInfo(serverDll).Parent.FullName
    // copy web assets into the server dll parent
    Directory.ensure (Path.Combine(serverDllParent, "public"))
    Shell.copyDir (Path.Combine(serverDllParent, "public")) (Path.Combine(deployPath, "public"))  (fun _ -> true)
    // re-create the nuget package
    ZipFile.CreateFromDirectory(Path.Combine(nugetParent, nugetFileName), nugetPackage)
    // delete intermediate directory
    Shell.deleteDir(Path.Combine(nugetParent, nugetFileName))
)

Target.create "Run" (fun _ ->
    run dotnet "build" sharedPath
    [ "server", dotnet "watch run" serverPath
      "client", dotnet "fable watch -o output -s --run npm run start" clientPath ]
    |> runParallel
)

Target.create "RunTests" (fun _ ->
    run dotnet "build" sharedTestsPath
    [ "server", dotnet "watch run" serverTestsPath
      "client", dotnet "fable watch -o output -s --run npm run test:live" clientTestsPath ]
    |> runParallel
)

Target.create "Format" (fun _ ->
    run dotnet "fantomas . -r" "src"
)

Target.create "GenerateAwsAncestorTypes" (fun _ ->
    let outputPath = Path.Combine(serverPath, "AwsAncestorTypes.fs")
    let awsSchemaVersion = "6.35.0"
    let content = Aws.generateLookupModule(awsSchemaVersion)
    File.WriteAllText(outputPath, content)
)

open Fake.Core.TargetOperators

let dependencies = [
    "Clean"
        ==> "InstallClient"

    "BuildClient"
      ==> "CreateArtifacts"

    "CleanArtifacts"
      ==> "CreateArtifacts"

    "InstallClient"
      ==> "BuildClient"
      ==> "CreateAndPublishArtifacts"

    "CleanArtifacts"
      ==> "CreateAndPublishArtifacts"

    "Clean"
        ==> "InstallClient"
        ==> "Run"

    "InstallClient"
        ==> "RunTests"

]

[<EntryPoint>]
let main args = runOrDefault args