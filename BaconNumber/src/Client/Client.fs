module Client

open Elmish
open Elmish.React
open Fable.React
open Fable.React.Props
open System
open Shared

// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type State = {
  SearchResult: List<Vertex>
  SearchText: Option<string>
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
  | Search
  | SearchCompleted of List<Vertex>
  | SearchFailed of Exception
  | SetSearchText of string

module Server =

    open Fable.Remoting.Client

    /// A proxy you can use to talk to server directly
    let api : ISearchApi =
      Remoting.createApi()
      |> Remoting.withRouteBuilder Route.builder
      |> Remoting.buildProxy<ISearchApi>

    let search text =
        Cmd.OfAsync.either
          api.search (SearchText(text))
          SearchCompleted
          SearchFailed

let initialState() =
    let initState = {
        SearchResult = []
        SearchText = None
    }

    initState, Cmd.none

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (msg: Msg) (prevState: State) =
    match msg with
    | SetSearchText text ->
        let nextState = { prevState with SearchText = Some text }
        nextState, Cmd.none

    | Search ->
        match prevState.SearchText with
        | None -> prevState, Cmd.none
        | Some text -> prevState, Server.search text

    | SearchCompleted items ->
        let nextState = { prevState with SearchResult = items; SearchText = None }
        nextState, Cmd.none

    | SearchFailed ex ->
        let nextState = { prevState with SearchText = Some "Failed..." }
        nextState, Cmd.none

let renderVertex (item: Vertex) dispatch =
    let style =
        match item.Type with
        | "cast" -> Style [ Color "Black"; FontSize 19; ]
        | "movie" -> Style [ Color "Gray"; FontSize 19; ]
        | _ -> failwith "Unknown type"

    div [ ]
        [ span [ style ] [ str item.Name ] ]


let search (state: State) dispatch =
  let textValue = defaultArg state.SearchText ""
  div
    [ ClassName "field has-addons"; Style [Padding 5; Width 400] ]
    [ div
        [ ClassName "control is-large" ]
        [ input [ ClassName "input is-large"
                  Placeholder "Enter name"
                  DefaultValue textValue
                  Value textValue
                  OnChange (fun ev -> dispatch (SetSearchText (ev.Value)))] ]
      div
        [ ClassName "control is-large" ]
        [ button [ ClassName "button is-primary is-large"; OnClick (fun _ -> dispatch Search) ] [ str "Search" ] ] ]

let view (state: State) dispatch =
    let vertexes =
      state.SearchResult
      |> List.map (fun vertex -> renderVertex vertex dispatch)

    let baconNumber =
        state.SearchResult
        |> List.filter (fun vertex -> vertex.Type = "movie")
        |> List.length
        |> string

    let baconNumberText = String.Format("The Bacon Number is {0}", baconNumber)

    div
     [ Style [ Padding 20 ] ]
     [ yield h1 [ Style [ FontWeight "Bold"; FontSize 24 ] ] [ str "Find Bacon Number" ]
       yield hr [ ]
       yield search state dispatch
       yield! vertexes
       yield hr [ ]
       yield h1 [ ] [ str baconNumberText ]
]

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram initialState update view
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactBatched "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
