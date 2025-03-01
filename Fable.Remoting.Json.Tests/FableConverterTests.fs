﻿module JsonConverterTests

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open Fable.Remoting.Json
open Expecto
open Types
open Expecto.Logging

let converter = new FableJsonConverter()
let settings = JsonSerializerSettings()

let deserialize<'a> (json : string) =
    if typeof<'a> = typeof<string> then unbox<'a> (box json)
    else JsonConvert.DeserializeObject(json, typeof<'a>, converter) :?> 'a
let serialize (value: 'a) = JsonConvert.SerializeObject(value, converter)

let equal x y = Expect.equal true (x = y) (sprintf "%A = %A" x y)
let pass() = Expect.equal true true ""
let fail () = Expect.equal false true ""
let converterTest =
    testList "Converter Tests" [
        testCase "DateTime conversion works" <| fun () ->
            let date = new DateTime(2017, 03, 23, 18, 30, 0)
            let serialized = serialize date
            let deserialized = deserialize<DateTime> serialized
            Expect.equal 30 deserialized.Minute "Minutes are equal"
            Expect.equal 18 deserialized.Hour "Hours are equal"
            Expect.equal 23 deserialized.Day "Days are equal"
            Expect.equal 3 deserialized.Month "Months are equal"
            Expect.equal 2017 deserialized.Year "Years are equal"

        testCase "Option<string> convertion works" <| fun () ->
            let opt = Some "value"
            let serialized = serialize opt
            let deserialized = deserialize<Option<string>> serialized
            match deserialized with
            | Some "value" -> pass()
            | otherwise -> fail()

        testCase "Union with DateTime conversion" <| fun () ->
            let dateInput = DateTime.Now.ToUniversalTime()
            let serialized = serialize (UnionWithDateTime.Date dateInput)
            let deserialized = deserialize<UnionWithDateTime> serialized
            match deserialized with
            | Int _ -> fail()
            | Date dateOutput ->
                Expect.equal dateInput.Second dateOutput.Second "Seconds are the same"
                Expect.equal dateInput.Minute dateOutput.Minute "Minutes are the same"
                Expect.equal dateInput.Hour dateOutput.Hour "Hours are the same"
                Expect.equal dateInput.Day dateOutput.Day "Days are the same"
                Expect.equal dateInput.Month dateOutput.Month "Months are the same"
                Expect.equal dateInput.Year dateOutput.Year "Year are the same"
                Expect.equal dateInput.Kind dateOutput.Kind "Kinds are the same"

        testCase "Single case union is deserialized correctly" <| fun () ->
          // assert that deserialization works
          let serialized = serialize { Id = CustomerId(5) }
          match deserialize<Customer> serialized with
          | { Id = CustomerId(5) } -> pass()
          | otherwise -> fail()

        testCase "Deserializing single case union of string from object" <| fun () ->
            let serialized = "{ \"Token\": \"Hello there\" }"
            match deserialize<Token> serialized with
            | Token "Hello there" -> pass()
            | otherwise -> fail()

        testCase "Deserializing single case union of string from array" <| fun () ->
            let serialized = "[\"Token\", \"Hello there\"]"
            match deserialize<Token> serialized with
            | Token "Hello there" -> pass()
            | otherwise -> fail()

        testCase "Deserializing single case union of string from Fable runtime representation" <| fun () ->
            let serialized = "{\"tag\":0, \"name\": \"Token\", \"fields\": [\"Hello there\"] }"
            match deserialize<Token> serialized with
            | Token "Hello there" -> pass()
            | otherwise -> fail()

        testCase "Single case union with long round trip" <| fun () ->
          let serialized = serialize (SingleLongCase 20L)
          match deserialize<SingleLongCase> serialized with
          | SingleLongCase 20L -> pass()
          | otherwise -> fail()

        testCase "String50 with private constructor can be serialized" <| fun ()  ->
            let serialized = "[\"String50\", \"onur\"]"
            let deserialized = deserialize<String50> serialized
            Expect.equal (deserialized.Read()) "onur" "Value is deserialized"

        testCase "Deserializing union of records using discriminiator" <| fun () ->
            let serialized = """
                [
                    {
                        "__typename": "User",
                        "Id": 42,
                        "Username": "John"
                    },

                    {
                        "__typename": "Bot",
                        "Identifier": "Sentient Bot"
                    }
                ]
            """

            let deserialized = deserialize<Actor list> serialized
            let wasFound actor = Expect.isTrue (List.contains actor deserialized) (sprintf "Actor %A was not found" actor)
            User { Id = 42; Username = "John" } |> wasFound
            Bot { Identifier = "Sentient Bot" } |> wasFound

        testCase "Deserializing union of records using lowe case discriminiator" <| fun () ->
            let serialized = """
                [
                    {
                        "__typename": "user",
                        "Id": 42,
                        "Username": "John"
                    },

                    {
                        "__typename": "bot",
                        "Identifier": "Sentient Bot"
                    }
                ]
            """

            let deserialized = deserialize<Actor list> serialized
            let wasFound actor = Expect.isTrue (List.contains actor deserialized) (sprintf "Actor %A was not found" actor)
            User { Id = 42; Username = "John" } |> wasFound
            Bot { Identifier = "Sentient Bot" } |> wasFound

        testCase "Map<int * int, int> can be deserialized" <| fun () ->
            let serialized = "[[[1,1],1]]"
            let deserialized =  deserialize<Map<int * int, int>> serialized
            match Map.toList deserialized with
            | [(1,1), 1] -> pass()
            | otherwise -> fail()

        testCase "Map<int * int, int> can be deserialized from object" <| fun () ->
            let serialized = "{ \"[1,1]\": 1 }"
            let deserialized =  deserialize<Map<int * int, int>> serialized
            match Map.toList deserialized with
            | [(1,1), 1] -> pass()
            | otherwise -> fail()

        testCase "Map<int * int, int> roundtrip" <| fun () ->
            let serialized = serialize (Map.ofList [(1,1), 1])
            let deserialized =  deserialize<Map<int * int, int>> serialized
            match Map.toList deserialized with
            | [(1,1), 1] -> pass()
            | otherwise -> fail()

        testCase "Int64 can be deserialized from high/low components" <| fun () ->
            let serialized = """{ "low": 20, "high": 0, "unsigned": true }"""
            match deserialize<int64> serialized with
            | 20L -> pass()
            | otherwise -> fail()

        testCase "Single case union without types is deserialized correctly" <| fun () ->
          let serialized = serialize { Color = ColorType Red }
          match deserialize<ColorRecord> serialized with
          | { Color = ColorType Red } -> pass()
          | otherwise -> fail()

        testCase "Option<int> conversion works" <| fun () ->
            let opt = Some 5
            let serialized = serialize opt
            let deserialized = deserialize<Option<int>> serialized
            match deserialized with
            | Some 5 -> pass()
            | otherwise -> fail()

        testCase "Option<int> deserialization from raw json works" <| fun () ->
            // what Fable outputs
            match deserialize<Option<int>> "5" with
            | Some 5 -> pass()
            | otherwise -> fail()

            match deserialize<Option<int>> "null" with
            | None -> pass()
            | otherwise -> fail()

        testCase "Nested options conversion works" <| fun () ->
            let nested = Some(Some (Some 5))
            let serialized = serialize nested
            equal "5" serialized
            let deserialized = deserialize<Option<Option<Option<int>>>> serialized
            match deserialized with
            | Some (Some (Some 5)) -> pass()
            | otherwise -> fail()

        testCase "Deserialize string from string works" <| fun () ->
            let input = "\"my-test-string\""
            let deserialized = deserialize<string> input
            equal input deserialized

        testCase "Record conversion works" <| fun () ->
            let input : Record = { Prop1 = "value"; Prop2 = 5; Prop3 = None }
            let deserialized = deserialize<Record> (serialize input)
            equal "value" deserialized.Prop1
            equal 5 deserialized.Prop2
            match deserialized.Prop3 with
            | None -> pass()
            | otherwise -> fail()

        testCase "Record deserialization from raw json works" <| fun () ->
            // let input : Record = { Prop1 = "value"; Prop2 = 5; Prop3 = None }
            // Fable serializes above record to:
            // "{\"Prop1\":\"value\",\"Prop2\":5,\"Prop3\":null}"
            let serialized = "{\"Prop1\":\"value\",\"Prop2\":5,\"Prop3\":null}"
            let deserialized = deserialize<Record> serialized
            equal "value" deserialized.Prop1
            equal 5 deserialized.Prop2
            match deserialized.Prop3 with
            | None -> pass()
            | otherwise -> fail()

        testCase "Generic union types conversion works" <| fun () ->
            let input = Just "value"
            let serialized = serialize input
            let deserialized = deserialize<Maybe<string>> serialized
            match deserialized with
            | Just "value" -> pass()
            | otherwise -> fail()

        testCase "Map<string, int> conversion works" <| fun () ->
            let input = ["one",1; "two",2] |> Map.ofSeq
            let serialized = serialize input
            let output = deserialize<Map<string, int>> serialized
            match Map.toList output with
            | ["one",1; "two",2] -> pass()
            | otherwise -> fail()

        testCase "Map<Guid, int> conversion works" <| fun () ->
            let firstGuid = Guid.NewGuid()
            let secondGuid = Guid.NewGuid()
            let input = Map.ofSeq [firstGuid, 1 ;secondGuid, 2]
            let serialized = serialize input
            let output =
                serialized
                |> deserialize<Map<Guid, int>>
                |> Map.toList
                |> List.sortBy snd

            match output with
            | [first,1; second,2] ->
                Expect.equal first firstGuid "First GUID is deserialized"
                Expect.equal second secondGuid "Second GUID is deserialized"
            | otherwise ->
                fail()

        testCase "Map<string, Option<string>> conversion works" <| fun () ->
            let input = ["one", Some 1; "two", Some 2] |> Map.ofSeq
            let serialized = serialize input
            let deserialized = deserialize<Map<string, Option<int>>> serialized
            match Map.toList deserialized with
            | ["one", Some 1; "two", Some 2] -> pass()
            | otherwise -> fail()

        test "DataTable can be converted" {
            let t = new System.Data.DataTable()
            t.TableName <- "myname"
            t.Columns.Add("a", typeof<int>) |> ignore
            t.Columns.Add("b", typeof<string>) |> ignore
            t.Rows.Add(1, "11111")  |> ignore
            t.Rows.Add(2, "222222") |> ignore
            let serialized = serialize t
            let deserialized = deserialize<System.Data.DataTable> serialized
            Expect.equal deserialized.Columns.Count   t.Columns.Count  "column count"
            Expect.equal deserialized.Rows.Count      t.Rows.Count     "row count"
            Expect.equal deserialized.TableName       t.TableName      "table name"
            Expect.equal deserialized.Rows.[0].["a"]  t.Rows.[0].["a"] "table.[0,'a']"
            Expect.equal deserialized.Rows.[0].["b"]  t.Rows.[0].["b"] "table.[0,'b']"
            Expect.equal deserialized.Rows.[1].["a"]  t.Rows.[1].["a"] "table.[1,'a']"
            Expect.equal deserialized.Rows.[1].["b"]  t.Rows.[1].["b"] "table.[1,'b']"
        }

        test "DataSet can be converted" {
            let t = new System.Data.DataTable()
            t.TableName <- "myname"
            t.Columns.Add("a", typeof<int>) |> ignore
            t.Columns.Add("b", typeof<string>) |> ignore
            t.Rows.Add(1, "11111")  |> ignore
            t.Rows.Add(2, "222222") |> ignore
            let ds = new System.Data.DataSet()
            ds.Tables.Add t
            let serialized = serialize ds

            let deserialized = deserialize<System.Data.DataSet> serialized
            Expect.equal deserialized.Tables.["myname"].Columns.Count   t.Columns.Count  "column count"
            Expect.equal deserialized.Tables.["myname"].Rows.Count      t.Rows.Count     "row count"
            Expect.equal deserialized.Tables.["myname"].TableName       t.TableName      "table name"
            Expect.equal deserialized.Tables.["myname"].Rows.[0].["a"]  t.Rows.[0].["a"] "table.[0,'a']"
            Expect.equal deserialized.Tables.["myname"].Rows.[0].["b"]  t.Rows.[0].["b"] "table.[0,'b']"
            Expect.equal deserialized.Tables.["myname"].Rows.[1].["a"]  t.Rows.[1].["a"] "table.[1,'a']"
            Expect.equal deserialized.Tables.["myname"].Rows.[1].["b"]  t.Rows.[1].["b"] "table.[1,'b']"
        }

        testCase "DateTimeOffset can be deserialized" <| fun () ->
            let input = "\"2019-04-01T16:00:00.000+05:00\""
            let deserialized = deserialize<DateTimeOffset> input
            let parsed = DateTimeOffset.Parse "2019-04-01T16:00:00.000+05:00"
            Expect.equal (deserialized.ToString()) (parsed.ToString()) "offsets should be the same"

        testCase "DateTimeOffset can be serialized correctly" <| fun () ->
            let value = DateTimeOffset.Parse "2019-04-01T16:00:00.000+05:00"
            let roundtripped = deserialize<DateTimeOffset> (serialize value)
            Expect.equal value roundtripped "offsets should be the same"

        testCase "Generic union types deserialization from raw json works" <| fun () ->
            // toJson (Just 5) = "{\"Just\":5}"
            // toJson Nothing = "\"Nothing\""
            // above is Fable output
            match deserialize<Maybe<int>> "{\"Just\":5}" with
            | Just 5 -> pass()
            | otherwise -> fail()

            match deserialize<Maybe<int>> "\"Nothing\"" with
            | Nothing -> pass()
            | otherwise -> fail()

            // Serialized "Nothing" is generic
            match deserialize<Maybe<string>> "\"Nothing\"" with
            | Nothing -> pass()
            | otherwise -> fail()

        testCase "Deserializing generic union types encoded as arrays" <| fun () ->
            match deserialize<Maybe<int>> "[\"Just\", 5]" with
            | Just 5 -> pass()
            | otherwise -> fail()

        testCase "Deserializing Map<string, int> from object literal works" <| fun () ->
            "{ \"firstKey\": 10, \"secondKey\": 20 }"
            |> deserialize<Map<string, int>>
            |> Map.toList
            |> function
                | [ "firstKey", 10; "secondKey", 20 ] -> pass()
                | otherwise -> fail()

        testCase "Deserializing optional timespan should work" <| fun () ->
            let data : OptionalTimeSpan = { value = Some (TimeSpan.FromHours 2.0) }
            let serialized = serialize data
            serialized
            |> deserialize<OptionalTimeSpan>
            |> equal data

        testCase "Deserializing struct DU should work" <| fun () ->
            let data : RecordWithStructDU = { value = StructDU "Hello" }
            let serialized = serialize data
            serialized
            |> deserialize<RecordWithStructDU>
            |> equal data

        testCase "Deserializing Map<int, int> from object literal works" <| fun () ->
            "{ \"10\": 10, \"20\": 20 }"
            |> deserialize<Map<int, int>>
            |> Map.toList
            |> function
                | [ 10, 10; 20, 20 ] -> pass()
                | otherwise -> fail()

        testCase "Deserializing Map<Color, int> from object literal works" <| fun () ->
            "{ \"Red\": 10, \"Blue\": 20 }"
            |> deserialize<Map<Color, int>>
            |> Map.toList
            |> fun output -> Expect.equal [ Color.Red, 10; Color.Blue, 20 ] output "deserialization worked"

        testCase "Map<Color, int> roundtrip" <| fun () ->
            let serialized =
              [ Color.Red, 10; Color.Blue, 20 ]
              |> Map.ofList
              |> serialize

            serialized
            |> deserialize<Map<Color, int>>
            |> Map.toList
            |> fun output -> Expect.equal [ Color.Red, 10; Color.Blue, 20 ] output "deserialization worked"

        testCase "Deserializing large int64 should work" <| fun () ->
            let content = "{ \"name\": \"FileName.exe\", \"size\": 23427026601 }"
            let deserialized = deserialize<File> content
            Expect.equal deserialized { name = "FileName.exe"; size = 23427026601L } "Deserialization worked"

        testCase "TestCommand can be converted correctly" <| fun () ->
            let firstGuid = Guid.NewGuid()
            let testCommand : TestCommand = {
                Data = {
                    CataA = "CataA"
                    CataC = "CataC"
                    CataB = Map.ofList [
                        firstGuid, {
                            MataA = "MataA"
                            MataC = "MataC"
                            MataB = Map.ofList [
                                firstGuid, { Text = "text"; Value = "value" }
                            ]
                        }
                    ]
                }
            }

            let serialized = serialize testCommand
            let deserialized = deserialize<TestCommand> serialized
            pass()

        testCase "TestCommand can be converted from JSON generated on the client" <| fun () ->
            let serialized = """
            {"Data": {"CataA": "CataA", "CataB": {"edd10cd5-3633-484f-b5db-8ff01fb88a5b": {"MataA": "MataA", "MataC": "MataC", "MataB": {"edd10cd5-3633-484f-b5db-8ff01fb88a5b": {"Text": "text", "Value": "value"}}}}, "CataC": "CataC"}}
            """
            let deserialized = deserialize<TestCommand> serialized
            pass()

        testCase "Deserializing map from array of arrays works" <| fun () ->
            "[[\"firstKey\", 10], [\"secondKey\", 20]]"
            |> deserialize<Map<string, int>>
            |> Map.toList
            |> function
                | [ "firstKey", 10; "secondKey", 20 ] -> pass()
                | otherwise -> fail()

        testCase "Deserializing map from array of arrays with complex types works" <| fun () ->
            "[[\"firstKey\", [\"Just\", 5]], [\"secondKey\", \"Nothing\"]]"
            |> deserialize<Map<string, Maybe<int>>>
            |> Map.toList
            |> function
                | [ "firstKey", Just 5; "secondKey", Nothing ] -> pass()
                | otherwise -> fail()

        testCase "Deserializing recursive union works - part 1" <| fun () ->
            "[\"Leaf\", 5]"
            |> deserialize<Tree<int>>
            |> function
                | Leaf 5 -> pass()
                | otherwise -> fail()

        testCase "Deserializing recursive union works - part 2" <| fun () ->
            "[\"Branch\", [\"Leaf\", 5], [\"Leaf\", 10]]"
            |> deserialize<Tree<int>>
            |> function
                | Branch(Leaf 5, Leaf 10) -> pass()
                | otherwise -> fail()

        testCase "Deserialization with provided type at runtime works" <| fun () ->
            let inputType = typeof<Option<int>>
            let json = "5"
            let parameterTypes = [| typeof<string>; typeof<System.Type> ; typeof<JsonConverter array> |]
            let deserialize = typeof<JsonConvert>.GetMethod("DeserializeObject", parameterTypes)
            equal true ((not << isNull) deserialize)

            let result = deserialize.Invoke(null, [| json; inputType; [| converter |] |])
            match result with
            | :? Option<int> as opt ->
                  match opt with
                  | Some 5 -> pass()
                  | otherwise -> fail()
            | otherwise -> fail()

        testCase "Deserialization DataTable" <| fun () ->
            let dataXml = """<?xml version="1.0" standalone="yes"?>
<NewDataSet>
  <myname>
    <a>1</a>
    <b>11111</b>
  </myname>
  <myname>
    <a>2</a>
    <b>222222</b>
  </myname>
</NewDataSet>"""
            let schemaXml = """<?xml version="1.0" standalone="yes"?>
            <xs:schema id="NewDataSet" xmlns="" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
              <xs:element name="NewDataSet" msdata:IsDataSet="true" msdata:MainDataTable="myname" msdata:UseCurrentLocale="true">
                <xs:complexType>
                  <xs:choice minOccurs="0" maxOccurs="unbounded">
                    <xs:element name="myname">
                      <xs:complexType>
                        <xs:sequence>
                          <xs:element name="a" type="xs:long" minOccurs="0" />
                          <xs:element name="b" type="xs:string" minOccurs="0" />
                          <xs:element name="c" type="xs:unsignedByte" minOccurs="0" />
                          <xs:element name="d" msdata:DataType="System.DateTimeOffset" type="xs:anyType" minOccurs="0" />
                          <xs:element name="e" type="xs:base64Binary" minOccurs="0" />
                          <xs:element name="f" msdata:DataType="System.DayOfWeek, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" type="xs:anyType" minOccurs="0" />
                          <xs:element name="g" type="xs:string" minOccurs="0" />
                          <xs:element name="h" type="xs:string" minOccurs="0" />
                          <xs:element name="i" type="xs:string" minOccurs="0" />
                        </xs:sequence>
                      </xs:complexType>
                    </xs:element>
                  </xs:choice>
                </xs:complexType>
              </xs:element>
            </xs:schema>"""
            let json = sprintf """{"schema": %s, "data" : %s}""" (serialize schemaXml) (serialize dataXml)
            let deserialized = deserialize<System.Data.DataTable> json
            equal deserialized.TableName       "myname"
            equal deserialized.Columns.Count   9
            equal deserialized.Rows.Count      2
            equal deserialized.Rows.[0].["a"]  (box 1L      )
            equal deserialized.Rows.[0].["b"]  (box "11111" )
            equal deserialized.Rows.[1].["a"]  (box 2L      )
            equal deserialized.Rows.[1].["b"]  (box "222222")

        testCase "Deserialization DataSet" <| fun () ->
            let dataXml =
              """<?xml version="1.0" standalone="yes"?>
<NewDataSet>
  <myname>
    <a>1</a>
    <b>11111</b>
  </myname>
  <myname>
    <a>2</a>
    <b>222222</b>
  </myname>
  <Table1>
    <a>aaa</a>
  </Table1>
</NewDataSet>"""
            let schemaXml =
              """<?xml version="1.0" standalone="yes"?>
<xs:schema id="NewDataSet" xmlns="" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
  <xs:element name="NewDataSet" msdata:IsDataSet="true" msdata:UseCurrentLocale="true">
    <xs:complexType>
      <xs:choice minOccurs="0" maxOccurs="unbounded">
        <xs:element name="myname">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="a" type="xs:long" minOccurs="0" />
              <xs:element name="b" type="xs:string" minOccurs="0" />
              <xs:element name="c" type="xs:unsignedByte" minOccurs="0" />
              <xs:element name="d" msdata:DataType="System.DateTimeOffset" type="xs:anyType" minOccurs="0" />
              <xs:element name="e" type="xs:base64Binary" minOccurs="0" />
              <xs:element name="f" msdata:DataType="System.DayOfWeek, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" type="xs:anyType" minOccurs="0" />
              <xs:element name="g" type="xs:string" minOccurs="0" />
              <xs:element name="h" type="xs:string" minOccurs="0" />
              <xs:element name="i" type="xs:string" minOccurs="0" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:element name="Table1">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="a" type="xs:string" minOccurs="0" />
            </xs:sequence>
          </xs:complexType>
        </xs:element>
      </xs:choice>
    </xs:complexType>
    <xs:unique name="Table1_Constraint1" msdata:ConstraintName="Constraint1">
      <xs:selector xpath=".//Table1" />
      <xs:field xpath="a" />
    </xs:unique>
    <xs:keyref name="Constraint1" refer="Table1_Constraint1" msdata:ConstraintOnly="true">
      <xs:selector xpath=".//myname" />
      <xs:field xpath="i" />
    </xs:keyref>
  </xs:element>
</xs:schema>"""

            let json = sprintf """{"schema": %s, "data" : %s}""" (serialize schemaXml) (serialize dataXml)
            let deserialized = deserialize<System.Data.DataSet> json
            equal deserialized.Tables.["myname"].Columns.Count   9
            equal deserialized.Tables.["myname"].Rows.Count      2
            equal deserialized.Tables.["myname"].Rows.[0].["a"]  (box 1L      )
            equal deserialized.Tables.["myname"].Rows.[0].["b"]  (box "11111" )
            equal deserialized.Tables.["myname"].Rows.[1].["a"]  (box 2L      )
            equal deserialized.Tables.["myname"].Rows.[1].["b"]  (box "222222")

    ]