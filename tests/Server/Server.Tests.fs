module Server.Tests

open Expecto

open Shared
open Server

let server = testList "Server" [ 
    test "A simple test" {
        let subject = "Hello World"
        Expect.equal subject "Hello World" "The strings should be equal"
    }
    test "using Maps" {
        let myMap = Map [("blah","boink"); ("foo","bar")]
        let myNestedMap = Map [("nested", myMap)]
        Expect.equal (myMap.TryGetValue "blah") (true, "boink") "That's not how maps work"
        Expect.equal (myMap.TryGetValue "forp") (false, null) "That's not how maps work"
        Expect.equal (myMap |> Map.tryFind "foo") (Some "bar") "That's not how maps work"
        Expect.equal myNestedMap.["nested"].["foo"] "bar" "That's not how maps work"
    }
    
]

let all =
    testList "All"
        [
            Shared.Tests.shared
            server
        ]

[<EntryPoint>]
let main _ = runTestsWithCLIArgs [] [||] all