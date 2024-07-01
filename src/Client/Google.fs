module Google

open Feliz
open Feliz.Router
open Feliz.UseDeferred
open Feliz.SelectSearch
open Shared

[<ReactComponent>]
let StartPage() =
    Html.div [
        MarkdownContent """
### Import from Google Cloud

The importer tool requires you to authenticate with Google Cloud using the CLI to import resources.

Use the [gcloud CLI](https://cloud.google.com/sdk/docs/install/) to login as follows:

```bash
gcloud auth application-default login
```

Then you can start importing resources from your Google Cloud account.

If you are already logged in, choose one of the following options to import resources:
"""

        Html.br [  ]
        Html.p [
            prop.style [ style.fontSize 18 ]
            prop.children [
                Html.a [
                    prop.href "/#google-asset-inventory"
                    prop.style [ style.marginRight 5 ]
                    prop.children [
                        Html.i [
                            prop.className "fa fa-chevron-right"
                            prop.style [ style.marginRight 5  ]
                        ]
                        Html.text "Google Cloud Asset Inventory"
                    ]
                ]

                Html.span "Query your resources by project using the Asset Inventory service."
            ]
        ]
    ]


[<ReactComponent>]
let SearchAssetInventory(project: GoogleProject) =
    let queryInputRef = React.useInputRef()
    let selectedMaxResult, setMaxResult = React.useState "100"
    let response, setResponse = React.useState(Deferred.HasNotStartedYet)
    let search = React.useDeferredCallback(Server.api.googleResourcesByProject, setResponse)
    React.fragment [
        MarkdownContent """
Search resources to import using the [query syntax](https://cloud.google.com/asset-inventory/docs/query-syntax)
for Cloud Asset Inventory. For example:
 - `state:("RUNNING" OR "ACTIVE" OR "ENABLED")` to get all active and running resources under this project
 - `location:us-west1` to get all resources in the `us-west1` location
 - `NOT state:(DESTROYED OR DESTROY_SCHEDULED) AND NOT location:global` to get all resources that are not destroyed and not in the global location
"""

        Html.div [
            prop.classes [ "field"; "is-grouped" ]
            prop.children [
                Html.p [
                    prop.classes [ "control"; "is-expanded" ]
                    prop.children [
                        Html.input [
                            prop.className "input"
                            prop.type' "text"
                            prop.placeholder "Query for searching all resources (can be left empty)"
                            prop.ref queryInputRef
                            prop.onKeyUp(key.enter, fun ev ->
                                queryInputRef.current
                                |> Option.iter (fun searchInput ->
                                    search {
                                        projectId = project.projectId
                                        query =
                                            if searchInput.value <> ""
                                            then Some searchInput.value
                                            else None
                                        maxResult = int selectedMaxResult
                                    })
                            )
                        ]
                    ]
                ]

                Html.p [
                    prop.className "control"
                    prop.children [
                        Html.button [
                            prop.classes [ "button"; "is-primary" ]
                            prop.text "Search"
                            prop.onClick (fun _ ->
                                queryInputRef.current
                                |> Option.iter (fun searchInput ->
                                    search {
                                        projectId = project.projectId
                                        query =
                                            if searchInput.value <> ""
                                            then Some searchInput.value
                                            else None
                                        maxResult = int selectedMaxResult
                                    })
                            )
                        ]
                    ]
                ]

                Html.p [
                    prop.className "control"
                    prop.children [
                        SelectSearch.selectSearch [
                            selectSearch.options [
                                { value = "100"; name = "Max Result - 100"; disabled = false }
                                { value = "500"; name = "Max Result - 500"; disabled = false }
                                { value = "1000"; name = "Max Result - 1000"; disabled = false }
                                { value = "1500"; name = "Max Result - 1500"; disabled = false }
                                { value = "2500"; name = "Max Result - 2500"; disabled = false }
                                { value = "5000"; name = "Max Result - 5000"; disabled = false }
                                { value = "10000"; name = "Max Result - 10000"; disabled = false }
                            ]
                            selectSearch.search false
                            selectSearch.value selectedMaxResult
                            selectSearch.onChange setMaxResult
                        ]
                    ]
                ]
            ]
        ]

        match response with
        | Deferred.HasNotStartedYet ->
            Html.none

        | Deferred.InProgress ->
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
            Html.p [
                prop.style [ style.color.red ]
                prop.text errorMessage
            ]

        | Deferred.Resolved (Ok resources) ->
            Html.p $"Found {resources.Length} resource(s)"
            Html.ul [
                for resource in resources ->
                Html.li [
                    Html.p $"Display Name: {resource.displayName}"
                    Html.p $"Type: {resource.resourceType}"
                    Html.p $"Full Name: {resource.name}"
                    Html.p $"Location: {resource.location}"
                    Html.p $"State: {resource.state}"
                ]
            ]
    ]



[<ReactComponent>]
let AssetInventory() =
    let selectedProjectId, setProjectId = React.useState<string option>(None)
    let response = React.useDeferred(Server.api.googleProjects(), [|  |])
    match response with
    | Deferred.HasNotStartedYet -> Html.none
    | Deferred.InProgress ->
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
        Html.p [
            prop.style [ style.color.red ]
            prop.text errorMessage
        ]

    | Deferred.Resolved (Ok projects) ->
        React.fragment [
            SelectSearch.selectSearch [
                selectSearch.options [
                    for project in projects -> {
                        value = project.projectId
                        name = project.projectName
                        disabled = false
                    }
                ]

                selectSearch.value (defaultArg selectedProjectId "")
                selectSearch.search true
                selectSearch.placeholder "Select a project to filter resources"
                selectSearch.onChange (Some >> setProjectId)
            ]

            Html.br [ ]

            match selectedProjectId with
            | None -> Html.none
            | Some projectId ->
                match projects |> List.tryFind (fun project -> project.projectId = projectId) with
                | None -> Html.none
                | Some project -> SearchAssetInventory project
        ]