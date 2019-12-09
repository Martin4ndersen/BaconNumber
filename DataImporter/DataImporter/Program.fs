open System
open FSharp.Data
open Microsoft.Azure.CosmosDB.BulkExecutor.Graph
open Microsoft.Azure.CosmosDB.BulkExecutor.Graph.Element
open Microsoft.Azure.Documents
open Microsoft.Azure.Documents.Client

type Movies = CsvProvider<"AllMoviesDetailsCleaned.csv", ";", IgnoreErrors=true>
type Cast = CsvProvider<"AllMoviesCastingRaw.csv", ";", IgnoreErrors=true>

let buildMovieGraphElements (movie: Movies.Row) =
    let vertex = GremlinVertex(movie.Id |> string, "movie")
    vertex.AddProperty("name", movie.Original_title)
    vertex |> box

let buildCastGraphElements (cast: Cast.Row) =
    let buildVertex actorName =
        let vertex = new GremlinVertex(actorName, "cast")
        vertex.AddProperty("name", actorName)
        vertex |> box

    let buildEdge actorName movieId =
        let edge = GremlinEdge(Guid.NewGuid().ToString(), "acted in", movieId, actorName, "movie", "cast")
        edge |> box
    
    let actorNames =
        [ cast.Actor1_name; cast.Actor2_name; cast.Actor3_name; cast.Actor4_name; cast.Actor5_name ]
        |> List.filter (fun actorName -> actorName <> "none")

    let vertexes =
        actorNames
        |> List.map buildVertex

    let movieId = cast.Id |> string
            
    let edges =
        actorNames
        |> List.map (buildEdge movieId)

    Seq.concat [ vertexes; edges ]

[<EntryPoint>]
let main argv =
    let movieGraphElements =
        Movies.Load("AllMoviesDetailsCleaned.csv").Rows
        |> Seq.map buildMovieGraphElements
        
    let castGraphElements =
        Cast.Load("AllMoviesCastingRaw.csv").Rows
        |> Seq.map buildCastGraphElements
        |> Seq.concat
            
    let serviceEndpoint = Uri("https://c747c2fd-0ee0-4-231-b9ee.documents.azure.com:443/");
    let authKey = "insert-key-here"
    
    let client = new DocumentClient(serviceEndpoint, authKey)
    client.CreateDatabaseIfNotExistsAsync(Database(Id = "database")) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
    let collection = client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("database"), DocumentCollection(Id = "graph")) |> Async.AwaitTask |> Async.RunSynchronously

    let graphBulkExecutor = GraphBulkExecutor(client, collection.Resource)
    graphBulkExecutor.InitializeAsync().Wait()
    
    let graphElements = Seq.concat [ movieGraphElements; castGraphElements; ]
    graphBulkExecutor.BulkImportAsync(graphElements = graphElements, enableUpsert = true) |> Async.AwaitTask |> Async.RunSynchronously |> ignore

    0 // return an integer exit code