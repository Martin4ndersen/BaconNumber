open System
open System.IO

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Shared

open FSharp.Data
open Fable.Remoting.Server
open Fable.Remoting.Giraffe

open Gremlin.Net.CosmosDb

type CosmosDb = JsonProvider<"""[ {"labels":[[],[],[],[],[],[],[]],"objects":[{"id":"Kevin Bacon","label":"cast","type":"vertex","properties":{"name":[{"id":"b3b4d4e1-0aea-45eb-ad65-98e24c46b2e7","value":"Kevin Bacon"}]}},{"id":"820","label":"movie","type":"vertex","properties":{"name":[{"id":"1905818b-6b69-4544-a938-6ad13e7f6700","value":"JFK"}]}},{"id":"Gary Oldman","label":"cast","type":"vertex","properties":{"name":[{"id":"595745da-9089-4298-8d32-09ffde9dc9df","value":"Gary Oldman"}]}},{"id":"272","label":"movie","type":"vertex","properties":{"name":[{"id":"cbd81597-e2e2-4c7f-abf5-037ded62afed","value":"Batman Begins"}]}},{"id":"Liam Neeson","label":"cast","type":"vertex","properties":{"name":[{"id":"ef390a17-0699-4029-8a9e-38998f03c291","value":"Liam Neeson"}]}},{"id":"411","label":"movie","type":"vertex","properties":{"name":[{"id":"7c854878-0586-4be0-bb51-59c66a1bc463","value":"The Chronicles of Narnia: The Lion, the Witch and the Wardrobe"}]}},{"id":"Anna Popplewell","label":"cast","type":"vertex","properties":{"name":[{"id":"cbff1ace-92af-419e-a2c0-12dd78f2ba78","value":"Anna Popplewell"}]}}]} ]""">

let tryGetEnv = System.Environment.GetEnvironmentVariable >> function null | "" -> None | x -> Some x

let publicPath = Path.GetFullPath "../Client/public"
let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us

let searchApi = {
    search = fun (SearchText(text)) -> async {
        let ToVertex (o: CosmosDb.Object) =
            let label = o.Label
            let name = o.Properties.Name.[0].Value
            let vertex = { Type = label; Name = name }
            vertex

        let host = "c747c2fd-0ee0-4-231-b9ee.gremlin.cosmos.azure.com"
        let authKey = "insert-key-here"
        let query = String.Format("g.V('{0}').repeat(out().in()).until(hasId('Kevin Bacon')).path().limit(1)", text)

        let graphClient = new GraphClient(host, "database", "graph", authKey)
        let response = graphClient.QuerySingleAsync(query) |> Async.AwaitTask |> Async.RunSynchronously
        let data = CosmosDb.Parse(response.ToString())

        let vertexes =
            data.[0].Objects
            |> Array.map ToVertex
            |> Array.toList

        return vertexes |> List.ofSeq
    }
}

let webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue searchApi
    |> Remoting.buildHttpHandler


let configureApp (app : IApplicationBuilder) =
    app.UseDefaultFiles()
       .UseStaticFiles()
       .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore

WebHost
    .CreateDefaultBuilder()
    .UseWebRoot(publicPath)
    .UseContentRoot(publicPath)
    .Configure(Action<IApplicationBuilder> configureApp)
    .ConfigureServices(configureServices)
    .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
    .Build()
    .Run()
