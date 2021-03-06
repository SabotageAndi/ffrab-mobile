﻿namespace www.linuxwochen.common

open System.Collections.ObjectModel
open ViewModule
open ViewModule.FSharp
open System
open System.Linq
open Xamarin.Forms
open NodaTime

module viewmodels = 
    open common
    open model
    open eventbus
    open entities
    
    type IViewModelShown = 
        abstract Init : unit -> unit
    
    type IRefresh =
        abstract RefreshCommand : INotifyCommand with get

    type AboutViewModel() as self = 
        inherit ViewModelBase()
              
        let onRead() =
            new eventbus.Entry(Message.ShowLicense) |> Eventbus.Current.Publish

        let onOpenSourceCode() =
            let uri = new Uri("https://github.com/SabotageAndi/at.linuxwochen.wien")
            Device.OpenUri(uri)

        let readCommand = self.Factory.CommandSync onRead
        let openSourceCodeCommand = self.Factory.CommandSync onOpenSourceCode

        member this.ReadCommand
            with get() = readCommand


        member this.OpenSourceCodeCommand
            with get() = openSourceCodeCommand
    
    type MenuItemConnection = 
        { Name : string
          Type : ViewModelType
          ViewModel : unit -> ViewModelBase
          Content : unit -> ContentView
          HasRefresh : bool }
    
    type MenuItemViewModel(menuItemConnection) as self = 
        inherit ViewModelBase()

        let isSelected = self.Factory.Backing(<@ self.IsSelected @>, false)

        let menuItemConnection = menuItemConnection
        member val Name = menuItemConnection.Name
        member val Type = menuItemConnection.Type

        member this.IsSelected 
            with get () = 
                isSelected.Value
            and set (v) = 
                isSelected.Value <- v
                self.RaisePropertyChanged(<@ self.BackgroundColor @>)
 

        member this.BackgroundColor
            with get () =
                if (this.IsSelected) then
                    Color.FromHex("#e1a61a")
                else
                    Color.FromHex("#DDDDDD")
    
    type SwitchPageEvent(msg, typ) =
        inherit eventbus.Entry(msg)

        member val Typ = typ

    type EntrySelected(msg, entry : entities.Entry) =
        inherit eventbus.Entry(msg)

        member val Entry = entry

    let getRoomName (room : entities.Room option) =
        match room with
        | Some room ->
            room.Name
        | None ->
            ""

    let initViewModel (viewModel : IViewModelShown) =
        viewModel.Init()

    type MenuViewModel() as self = 
        inherit ViewModelBase()
        let items = self.Factory.Backing(<@ self.Items @>, new ObservableCollection<MenuItemViewModel>())
        let selectedItem = self.Factory.Backing(<@ self.SelectedItem @>, items.Value.FirstOrDefault())
        
        let removeMenuEntry entry =
            items.Value.Remove(entry) |> ignore

        member this.AddMenu item = 
            let itemViewModel = new MenuItemViewModel(item)
            items.Value.Add itemViewModel
        
        member this.AddMenuAfter after item = 
            let index = 
                items.Value
                |> List.ofSeq
                |> List.findIndex (fun i -> i.Type = after.Type)
            items.Value.Insert(index + 1, new MenuItemViewModel(item))
        
        member this.RemoveMenu name = 
            items.Value
            |> List.ofSeq
            |> List.filter (fun i -> i.Name = name)
            |> List.iter removeMenuEntry
        
        member val Items = items.Value with get
        
        member this.SelectedItem 
            with get () = selectedItem.Value
            and set (v) = 
                selectedItem.Value <- v
                new SwitchPageEvent(Message.SwitchPage, selectedItem.Value.Type) |> Eventbus.Current.Publish
        
        member this.SetCurrentItem item = 

            items.Value
                |> List.ofSeq
                |> List.iter (fun i -> i.IsSelected <- false)
                    
            let mivm = 
                items.Value
                |> List.ofSeq
                |> List.tryFind (fun i -> i.Type = item.Type && i.Name = item.Name)
                        
            match mivm with
            | Some x -> 
                x.IsSelected <- true
                selectedItem.Value <- x
            | _ -> ignore()
    
    type ConferenceListViewModel() as self = 
        inherit ViewModelBase()
        let items = self.Factory.Backing(<@ self.Items @>, new ObservableCollection<Conference>())
        let selectedItem = self.Factory.Backing(<@ self.SelectedItem @>, None)
        
        interface IViewModelShown with
            member this.Init() = 
                items.Value <- new ObservableCollection<Conference>(Conferences.getAllConferences())
        

        member val Items = items.Value with get
        
        member this.SelectedItem 
            with get () = 
                match selectedItem.Value with
                | Some v -> v
                | _ -> null
            and set (v) = 
                match v with
                | null -> selectedItem.Value <- None
                | _ -> 
                    selectedItem.Value <- Some v
                    model.Conferences.setActualConference selectedItem.Value.Value
                    new eventbus.Entry(Message.ChangeConference) |>  Eventbus.Current.Publish
 
    [<AllowNullLiteralAttribute>]
    type FavoriteItemViewModel(entry : entities.Entry) =

        let entry = entry

        member val Entry = entry
        member val Title = entry.Title
        member val Room = queries.getRoom entry.RoomGuid |> getRoomName
            
        member this.BeginTime 
            with get() =
                let formatedStartTime = common.Formatting.timeOffsetFormat.Format entry.Start
                match entry.Start.Date = SystemClock.Instance.Now.InZone(DateTimeZoneProviders.Tzdb.GetSystemDefault()).Date with
                | true ->                    
                    formatedStartTime
                | false ->
                    sprintf "%s - %s" formatedStartTime (common.Formatting.dateOffsetFormat.Format entry.Start)
    
    [<AllowNullLiteralAttribute>]
    type EntryItemViewModel(entry : entities.Entry) =

        let entry = entry

        member val Entry = entry
        member val Title = entry.Title
        member val BeginTime = common.Formatting.timeOffsetFormat.Format entry.Start

    type MainViewModel() as self = 
        inherit ViewModelBase()

        let nextEvents = self.Factory.Backing(<@ self.NextEvents @>, new ObservableCollection<FavoriteItemViewModel>())
        let nextFavoriteEvents = self.Factory.Backing(<@ self.NextFavoriteEvents @>, new ObservableCollection<FavoriteItemViewModel>())
        let selectedFavoriteItem = self.Factory.Backing(<@ self.SelectedFavoriteItem @>, None)
        let selectedEventItem = self.Factory.Backing(<@ self.SelectedEventItem @>, None)
       

        let updateLists() =
            nextFavoriteEvents.Value.Clear()
            nextEvents.Value.Clear()

            model.Conferences.getActualConference()
            |> queries.getTopFavorites 5
            |> List.map (fun e -> new FavoriteItemViewModel(e))
            |> List.iter nextFavoriteEvents.Value.Add

            model.Conferences.getActualConference()
            |> queries.getNextTalks 5
            |> List.map (fun e -> new FavoriteItemViewModel(e))
            |> List.iter nextEvents.Value.Add

            self.RaisePropertyChanged(<@ self.NextFavoriteEventsVisible @>)



        let refresh() =
            async {
                model.SyncWithUi()
                |> ignore

                updateLists |> common.runOnUIthread

                endLongRunningTask()
            } |> Async.Start

           
           
        let refreshCommand = self.Factory.CommandSync(refresh)
        
        interface IViewModelShown with
            member this.Init() = 
                updateLists()

        
        interface IRefresh with
            member this.RefreshCommand = refreshCommand

        member this.NextFavoriteEvents = nextFavoriteEvents.Value
        member this.NextEvents = nextEvents.Value

        member this.NextFavoriteEventsVisible 
            with get() = this.NextFavoriteEvents.Any()

        member this.SelectedFavoriteItem 
            with get () : FavoriteItemViewModel = 
                match selectedFavoriteItem.Value with
                | Some v -> v
                | _ -> null
            and set (v) = 
                match v with
                | null -> selectedFavoriteItem.Value <- None
                | _ -> 
                    selectedFavoriteItem.Value <- Some v
                    new EntrySelected(Message.ShowEntry, v.Entry) |> Eventbus.Current.Publish

        member this.SelectedEventItem 
            with get () : FavoriteItemViewModel = 
                match selectedEventItem.Value with
                | Some v -> v
                | _ -> null
            and set (v) = 
                match v with
                | null -> selectedEventItem.Value <- None
                | _ -> 
                    selectedEventItem.Value <- Some v
                    new EntrySelected(Message.ShowEntry, v.Entry) |> Eventbus.Current.Publish


    

    [<AllowNullLiteralAttribute>]
    type GroupDayItemViewModel(startTime : OffsetDateTime, itemViewModels : EntryItemViewModel list) =
        inherit ObservableCollection<EntryItemViewModel>(itemViewModels)

        let startTime = startTime

        member val StartTime = common.Formatting.timeOffsetFormat.Format startTime

        
    type DayViewModel(conferenceDay) as self =
        inherit ViewModelBase()
        let items = self.Factory.Backing(<@ self.Items @>, new ObservableCollection<GroupDayItemViewModel>())
        let selectedItem = self.Factory.Backing(<@ self.SelectedItem @>, None)
        
        let conferenceDay = conferenceDay

        interface IViewModelShown with
            member x.Init() =
                let viewModels = conferenceDay
                                 |> queries.getEntriesForDay
                                 |> List.groupBy (fun e -> e.Start)
                                 |> List.map (fun (key, value) -> (key, value |> List.map (fun i -> new EntryItemViewModel(i))))
                                 |> List.map (fun (key, value) -> new GroupDayItemViewModel(key, value))
                                 
                items.Value <- new ObservableCollection<GroupDayItemViewModel>(viewModels)

        member this.Items = items.Value

        member self.SelectedItem 
            with get () : EntryItemViewModel = 
                match selectedItem.Value with
                | Some v -> v
                | _ -> null
            and set (v) = 
                match v with
                | null -> selectedItem.Value <- None
                | _ -> 
                    selectedItem.Value <- Some v
                    new EntrySelected(Message.ShowEntry, v.Entry) |> Eventbus.Current.Publish
                    

    type EntryViewModel(entry : entities.Entry) as self =
        inherit ViewModelBase()

        let onFavorite() =
            model.Entry.toggleEntryFavorite entry
            self.RaisePropertyChanged <@ self.FavoriteIcon @>

        let favoriteCommand = self.Factory.CommandSync onFavorite
        let entry = entry
        let mutable room : entities.Room option = None
        let mutable speaker :entities.Speaker list = []

        interface IViewModelShown with
            member this.Init() =
                room <- queries.getRoom entry.RoomGuid
                speaker <- queries.getSpeakersOfEntry entry

        member val Title = entry.Title with get
        member val BeginTime = common.Formatting.timeOffsetFormat.Format entry.Start with get
        member val Duration = common.Formatting.durationFormat.Format entry.Duration with get

        member this.Time
            with get() = sprintf "%s - %s min" this.BeginTime this.Duration

        member this.Room 
            with get() =
                getRoomName room
        member val Track = entry.Track with get

        member this.Abstract
            with get() = entry.Abstract
           
        member this.Description
            with get() = entry.Description

        member this.Speaker 
            with get() = speaker |> List.map (fun s -> s.Name) |> String.concat ", "

        member this.FavoriteCommand 
            with get() = favoriteCommand

        member this.FavoriteIcon
            with get() =
                match model.Entry.isEntryFavorite entry with
                | true ->
                    "ic_star_black_36dp.png"
                | false ->
                    "ic_star_border_black_36dp.png"
