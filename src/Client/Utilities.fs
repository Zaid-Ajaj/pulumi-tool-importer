[<AutoOpen>]
module Utilities

open Feliz
open Feliz.Markdown
open Fable.Core

[<ReactComponent>]
let Div(className: string, children: ReactElement list) =
    Html.div [
        prop.className className
        prop.children children
    ]

[<Emit "navigator.clipboard.writeText($0)">]
let copyToClipboard (text: string) : unit = jsNative

[<ReactComponent(import="default", from="react-highlight")>]
let Highlight(className: string, children: ReactElement array) : ReactElement = jsNative

[<ReactComponent>]
let MarkdownContent(sourceMarkdown: string) =
    Div("content", [
        Markdown.markdown [
            markdown.children sourceMarkdown
            markdown.components [
                markdown.components.pre (fun props -> React.fragment props.children)
                markdown.components.code (fun props ->
                    if props.isInline
                    then Html.code props.children
                    else Highlight(props.className, props.children)
                )
            ]
        ]
    ])

[<ReactComponent>]
let Tab(title: string, value: string, selectedTab, onClick: string -> unit) =
    Html.li [
        prop.className (if selectedTab = value then "is-active" else "")
        prop.children [
            Html.a [
                prop.onClick (fun _ -> onClick value)
                prop.text title
            ]
        ]
    ]

let Tabs(children: ReactElement list) =
    Div("tabs", [
        Html.ul children
    ])

[<ReactComponent>]
let ImportJsonDocs(region: string) =
    let command =
        if region <> "__default__" && region <> ""
        then $"\nRemember to setup your Pulumi stack with the AWS region using `pulumi config set aws:region {region}` before running the import\n"
        else ""

    MarkdownContent $"""
The following JSON content can be used by pulumi to batch import the resources into a pulumi project.

Put the contents inside a file called `import.json` and run `pulumi import -f import.json`
{command}
See [pulumi import](https://www.pulumi.com/docs/cli/commands/pulumi_import/) to learn more about importing resources.
"""

