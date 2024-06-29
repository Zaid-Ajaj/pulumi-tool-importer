[<RequireQualifiedAccess>]
module Import

open Feliz
open Feliz.SelectSearch
open Feliz.UseDeferred

[<ReactComponent>]
let AwsRegionImportDocs(region:string) = MarkdownContent $"""
Note: we run import previews with environment variable AWS_REGION={region}
Remember to setup your Pulumi stack with the correct AWS region using `pulumi config set aws:region {region}`
when you actually run the import command.
"""

[<ReactComponent>]
let Preview(importJson: string, region: string) =
    let importJsonInputRef = React.useInputRef()
    let tab, setTab = React.useState "code"
    let language, setLanguage = React.useState "typescript"
    let preview, setPreview = React.useState(Deferred.HasNotStartedYet)
    let importPreview = React.useDeferredCallback(Server.api.importPreview, setPreview)
    React.fragment [
        Html.div [
            prop.style [
                style.width 200
                style.display.inlineBlock
                style.position.relative
            ]
            prop.children [
                SelectSearch.selectSearch [
                    selectSearch.options [
                        { value = "typescript"; name = "TypeScript"; disabled = false }
                        { value = "python"; name = "Python"; disabled = false }
                        { value = "go"; name = "Go"; disabled = false }
                        { value = "csharp"; name = "C#"; disabled = false }
                        { value = "yaml"; name = "YAML"; disabled = false }
                        { value = "java"; name = "Java"; disabled = false }
                    ]

                    selectSearch.value language
                    selectSearch.search true
                    selectSearch.placeholder "Select a language to preview the import"
                    selectSearch.onChange setLanguage
                ]
            ]
        ]

        Html.div [
            prop.style [
                style.width 200
                style.display.inlineBlock
                style.position.relative
                style.marginLeft 20
            ]
            prop.children [
                Html.button [
                    prop.text "Start import preview"
                    prop.className "button is-primary"
                    prop.style [ style.height 36; style.top -5 ]
                    prop.disabled (Deferred.inProgress preview)
                    prop.onClick (fun _ ->
                        importJsonInputRef.current
                        |> Option.iter (fun importJsonInput ->
                            importPreview {
                                language = language
                                pulumiImportJson = importJsonInput.value
                                region = region
                            }))
                ]
            ]
        ]

        Html.br [ ]

        if region <> "__default__" && region <> ""
            then AwsRegionImportDocs region

        Html.details [
            prop.style [
                style.marginTop 10
                style.marginBottom 10
            ]

            prop.children [
                Html.summary "Edit Pulumi Import JSON:"
                Html.div [
                    prop.className "control"
                    prop.children [
                         Html.textarea [
                            prop.style [ style.height 350 ]
                            prop.className "input"
                            prop.ref importJsonInputRef
                            prop.defaultValue importJson
                        ]
                    ]
                ]
            ]
        ]

        match preview with
        | Deferred.HasNotStartedYet ->
            Html.none
        | Deferred.InProgress ->
            Html.p $"Generating import preview in {language}"
            Html.progress [
                prop.className "progress is-small is-primary"
                prop.max 100
            ]

        | Deferred.Failed error ->
            Html.p [
                prop.style [ style.color.red ]
                prop.text error.Message
            ]

        | Deferred.Resolved (Error errorMessage) ->
            let parts = errorMessage.Split "\n"
            React.fragment [
                for part in parts do
                Html.p [
                    prop.style [ style.color.red ]
                    prop.text part
                ]
            ]

        | Deferred.Resolved (Ok response) ->
            Tabs [
                Tab("Code", "code", tab, setTab)
                Tab("Stack state", "stack-state", tab, setTab)
                if not (List.isEmpty response.warnings) then
                    Tab($"Warnings ({List.length response.warnings})", "warnings", tab, setTab)
                match response.standardError with
                | Some error -> Tab("Import Errors", "error", tab, setTab)
                | None -> ()
            ]

            match tab with
            | "code" ->
                Highlight(language, [| Html.code response.generatedCode |])
            | "stack-state" ->
                Html.pre response.stackState
            | "warnings" ->
                Html.ul [
                    for warning in response.warnings do
                    Html.li warning
                ]
            | "error" ->
                Html.p [
                    prop.style [ style.color.red ]
                    prop.text (Option.defaultValue "" response.standardError)
                ]
            | _ ->
                Html.none
    ]