﻿module Database 

    open Xunit
    open FsUnit.Xunit
    open System
    open System.IO
    open SQLite.Net
    open ffrab.mobile.common.model
    open ffrab.mobile.common.entities
    open ffrab.mobile.common

    let conf = new Conference(1, "", "", "")

    let init() =
        let db = new SQLite.Net.Platform.Win32.SQLitePlatformWin32()
        Init(db, "ffrab.mobile.db")
        ffrab.mobile.common.common.CurrentState.SQLConnection

    let initWithRecreate() =
        let db = init()
        Database.dropSchema()
        Database.createSchema()

        db

    [<Fact>]
    let ``Database is recreated``() =
        let db = init()
        Database.createSchema()
    
    [<Fact>]
    let ``Entries removed on conference clean``() =
        let db = initWithRecreate()

        let entry = new Entry(ConferenceId = 1)
        db.Insert entry |> should equal 1
        
        db.Table<Entry>().Count() |> should equal 1

        queries.deleteConference conf

        db.Table<Entry>().Count() |> should equal 0

    [<Fact>]
    let ``Rooms removed on conference clean``() =
        let db = initWithRecreate()

        let room = new Room(ConferenceId = 1)
        db.Insert room |> should equal 1

        db.Table<Room>().Count() |> should equal 1

        queries.deleteConference conf

        db.Table<Room>().Count() |> should equal 0

    [<Fact>]
    let ``Conference Day removed on conference clean``() =
        let db = initWithRecreate()

        let conferenceDay = new ConferenceDay(ConferenceId = 1)
        db.Insert conferenceDay |> should equal 1

        db.Table<ConferenceDay>().Count() |> should equal 1

        queries.deleteConference conf

        db.Table<ConferenceDay>().Count() |> should equal 0

    [<Fact>]
    let ``ConferenceData removed on conference clean``() =
        let db = initWithRecreate()

        let conferenceData = new ConferenceData(ConferenceId = 1)
        db.Insert conferenceData |> should equal 1

        db.Table<ConferenceData>().Count() |> should equal 1

        queries.deleteConference conf

        db.Table<ConferenceData>().Count() |> should equal 0
