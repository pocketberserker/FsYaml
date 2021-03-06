﻿module YamlDumper

open System
open System.Text.RegularExpressions

open Microsoft.FSharp.Reflection

open Patterns
open ReflectionUtils

let dumpString str =
  let pat = @"[\b\r\n\t""]"
  if Regex.IsMatch(str, pat) then
    "\"" + Regex.Replace(str, "\"", @"\""") + "\""
  else
    str

let dumpFloat f =
  let (|Infinity|NegativeInfinity|NaN|Float|)f =
    if Double.IsNaN f then NaN
    else if f = infinity then Infinity
    else if f = -infinity then NegativeInfinity
    else Float f

  match f with
  | Infinity -> ".inf"
  | NegativeInfinity -> "-.inf"
  | NaN -> ".nan"
  | Float f -> string f

let dumpPrimitive level (x: obj) =
  match x.GetType() with
  | StrType -> dumpString (x :?> string)
  | IntType -> string x
  | FloatType -> dumpFloat (x :?> float)
  | DecimalType -> string x
  | BoolType -> x |> string |> Str.lower
  | _ -> failwith "未実装なんですけど"

let recordValues x =
  FSharpType.GetRecordFields (x.GetType())
  |> Seq.map (fun info -> info.Name :> IComparable, info.GetValue(x, null))
  |> List.ofSeq

let dumpBlock dump level values =
  let result =
    values
    |> List.map dump
    |> Str.join "\n"
  if level = 0 then
    result
  else
    "\n" + result

type Paren = { OpenParen: string; CloseParen: string; }
let makeParen o c = { OpenParen = o; CloseParen = c; }

let dumpInline dump paren values =
  let result =
    values
    |> List.map dump
    |> Str.join ", "
  paren.OpenParen + " "  + result + " " + paren.CloseParen

let dumpNull _ _ = "null"

let rec dumpRecord level x =
  let dumpBlockMap level =
    let map (name, value) =
      new String(' ', level * 2) + (string name) + ": " + (dump (level + 1) value)
    dumpBlock map level
  x
  |> recordValues 
  |> dumpBlockMap level 
and dumpMap level x = 
  let dumpBlockMap level =
    let map (name, value) =
      new String(' ', level * 2) + (string name) + ": " + (dump (level + 1) value)
    dumpBlock map level
  let dumpInlineMap level =
    let map (name, value) = (string name) + ": " + (dump (level + 1) value)
    dumpInline map (makeParen "{" "}")

  let values = x |> normalizeMap |> Map.toList
  x.GetType().GetGenericArguments().[1]
  |> function
     | Patterns.PrimitiveType ->
         values
         |> dumpInlineMap level
     | _ ->
         values
         |> dumpBlockMap level
and dumpList level x =
  let dumpBlockList level =
    let list value =
      new String(' ', level * 2) + "- " + (dump (level + 1) value)
    dumpBlock list level
  let dumpInlineList level =
    let list value = (dump (level + 1) value)
    dumpInline list (makeParen "[" "]")

  let values = x |> normalizeList
  x.GetType().GetGenericArguments().[0]
  |> function
     | PrimitiveType ->
         values
         |> dumpInlineList level
     | _ ->
         values
         |> dumpBlockList level
and dumpOption level x =
  x
  |> normalizeOption
  |> function
     | Some x -> dump level x
     | None -> "null"
and dumpUnion level x =
  let t = x.GetType();
  let info, objs = FSharpValue.GetUnionFields(x, t)
  info.Name + ": " +
    match objs with
    | [||] -> ""
    | [| x |] -> dump (level + 1) x
    | _ -> failwith "2項以上はサポート外です。"
and dump level (x: obj) =
  let dump' =
    if x = null then
      dumpNull
    else
      x.GetType()
      |> function
         | PrimitiveType -> dumpPrimitive
         | RecordType _ -> dumpRecord
         | MapType _ -> dumpMap
         | ListType _ -> dumpList
         | OptionType _ -> dumpOption
         | UnionType _ -> dumpUnion
  dump' level x