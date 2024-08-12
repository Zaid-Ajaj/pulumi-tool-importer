module Yaml

open YamlDotNet
open System.IO
open Newtonsoft.Json.Linq
open YamlDotNet.Serialization

let convertToJson (inputYaml: string) : JObject =
    let reader = new StringReader(inputYaml)
    let deserializer =
        DeserializerBuilder()
            .WithTagMapping("!Ref", typeof<string>)
            .WithTagMapping("!GetAtt", typeof<string>)
            .WithTagMapping("!Sub", typeof<string>)
            .Build()

    let yamlObject = deserializer.Deserialize(reader)
    let serializer = SerializerBuilder().JsonCompatible().Build()
    JObject.Parse(serializer.Serialize yamlObject)