open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System
open System.IO
open System.Text
open System.Text.Json
open System.Threading.Tasks
open VezCheckClean.Checklists

[<CLIMutable>]
type ChecklistResult = {
    ChecklistType : string
    Date          : DateTime
    OperatorName  : string
    FinalComment  : string
    Statuses      : Map<int,string>
}

let getTitle = function
    | "daily"   -> "Daily Checklist"
    | "weekly"  -> "Weekly Checklist"
    | "monthly" -> "Monthly Checklist"
    | _         -> "Unknown Checklist"

let commonStyle =
    """
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600&display=swap');

    body {
      font-family: 'Inter', sans-serif;
      background-color: #0d1117;
      color: #c9d1d9;
      margin: 0;
      padding: 20px;
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
    }

    .form-container {
      background: #161b22;
      padding: 40px;
      border-radius: 16px;
      box-shadow: 0 4px 12px rgba(0,0,0,0.6);
      width: 100%;
      max-width: 700px;
    }

    h1, h2, h3 {
      margin-top: 0;
      color: #f0f6fc;
    }

    label {
      display: block;
      margin-top: 20px;
      font-weight: 600;
      color: #8b949e;
    }

    select, input[type="text"], input[type="date"], textarea {
      width: 100%;
      padding: 12px;
      margin-top: 8px;
      border: 1px solid #30363d;
      border-radius: 8px;
      background-color: #0d1117;
      color: #c9d1d9;
      box-sizing: border-box;
      font-size: 15px;
    }

    textarea {
      min-height: 100px;
      resize: vertical;
    }

    button {
      background: linear-gradient(135deg, #238636, #2ea043);
      color: #ffffff;
      border: none;
      padding: 12px 24px;
      border-radius: 8px;
      cursor: pointer;
      margin-top: 20px;
      font-size: 16px;
      transition: background 0.3s ease;
    }

    button:hover {
      background: linear-gradient(135deg, #2ea043, #238636);
    }

    ul {
      padding-left: 20px;
    }

    li {
      margin-bottom: 10px;
      color: #adbac7;
      line-height: 1.5;
    }

    @media (max-width: 640px) {
      .form-container {
        padding: 20px;
      }
    }
  </style>
"""

let renderIndex () =
    $"""
<html>
<head>
  <title>VezCheck</title>
  {commonStyle}
</head>
<body>
  <div class="form-container">
    <h1>Welcome to VezCheck</h1>
    <form method="get" action="/step">
      <label>Select checklist:</label>
      <select name="type">
        <option value="daily">Daily</option>
        <option value="weekly">Weekly</option>
        <option value="monthly">Monthly</option>
      </select>
      <input type="hidden" name="index" value="0" />
      <button type="submit">Start</button>
    </form>
  </div>
</body>
</html>
"""

let renderStepPage (checklistType: string) (index: int) (statuses: Map<int,string>) (comment: string) =
    let title = getTitle checklistType
    let list = getChecklist checklistType
    let sb = StringBuilder()

    let countByStatus (statuses: Map<int, string>) =
        statuses
        |> Seq.map (fun kv -> kv.Value.Split("|||").[0])
        |> Seq.countBy id
        |> Map.ofSeq

    let statusCounts = countByStatus statuses
    let getCount key = statusCounts.TryFind key |> Option.defaultValue 0

    sb.AppendLine("<html>") |> ignore
    sb.AppendLine("<head>") |> ignore
    sb.AppendLine(sprintf "<title>%s</title>" title) |> ignore
    sb.AppendLine(commonStyle) |> ignore
    sb.AppendLine("""<style>
      .summary {
        display: flex;
        justify-content: space-between;
        margin-bottom: 20px;
        gap: 40px;
      }
      .box {
        flex: 1;
        background-color: #1c2128;
        padding: 20px;
        border-radius: 12px;
        box-shadow: 0 2px 8px rgba(0,0,0,0.4);
      }
      .bar-wrapper {
        margin: 30px 0;
        text-align: center;
      }
      .bar-container {
        display: inline-block;
        width: 80%;
        background-color: #30363d;
        border-radius: 6px;
        overflow: hidden;
        height: 20px;
      }
      .bar-fill {
        height: 100%;
        float: left;
      }
      .ok { background-color: #2ea043; }
      .fail { background-color: #da3633; }
      .error { background-color: #d29922; }
      .na { background-color: #6e7681; }
      @media print {
        .summary, .bar-wrapper { display: none; }
      }
    </style>""") |> ignore
    sb.AppendLine("</head>") |> ignore
    sb.AppendLine("<body>") |> ignore
    sb.AppendLine("<div class=\"form-container\">") |> ignore
    sb.AppendLine(sprintf "<h2>%s</h2>" title) |> ignore

    if index >= list.Length then
        let total = list.Length |> float
        let ok = getCount "OK"
        let fail = getCount "FAIL"
        let err = getCount "ERROR"
        let na = getCount "NA"
        let sum = ok + fail + err + na |> float

        sb.AppendLine("<div class=\"summary\">") |> ignore
        sb.AppendLine("<div class=\"box\">") |> ignore
        sb.AppendLine("<h3>Status Summary</h3>") |> ignore
        sb.AppendLine(sprintf "<p>OK: %d</p>" ok) |> ignore
        sb.AppendLine(sprintf "<p>FAIL: %d</p>" fail) |> ignore
        sb.AppendLine(sprintf "<p>ERROR: %d</p>" err) |> ignore
        sb.AppendLine(sprintf "<p>NA: %d</p>" na) |> ignore
        sb.AppendLine("</div></div>") |> ignore

        let percent n = if sum = 0.0 then 0.0 else (float n) / sum * 100.0
        sb.AppendLine("<div class=\"bar-wrapper\"><div class=\"bar-container\">") |> ignore
        sb.AppendLine(sprintf "<div class=\"bar-fill ok\" style=\"width: %.1f%%\"></div>" (percent ok)) |> ignore
        sb.AppendLine(sprintf "<div class=\"bar-fill fail\" style=\"width: %.1f%%\"></div>" (percent fail)) |> ignore
        sb.AppendLine(sprintf "<div class=\"bar-fill error\" style=\"width: %.1f%%\"></div>" (percent err)) |> ignore
        sb.AppendLine(sprintf "<div class=\"bar-fill na\" style=\"width: %.1f%%\"></div>" (percent na)) |> ignore
        sb.AppendLine("</div></div>") |> ignore

        sb.AppendLine("<h3>Checklist Complete!</h3><ul>") |> ignore
        for i in 0 .. list.Length - 1 do
            let task = list.[i]
            let raw = statuses.TryFind i |> Option.defaultValue "Not answered"
            let parts = raw.Split("|||")
            let cmt = if parts.Length > 1 then parts.[1] else ""
            let line = if cmt.Trim() = "" then task else sprintf "%s (%s)" task cmt
            sb.AppendLine(sprintf "<li>%s</li>" line) |> ignore
        sb.AppendLine("</ul>") |> ignore

        sb.AppendLine("<form method=\"post\" action=\"/save\">") |> ignore
        sb.AppendLine(sprintf "<input type=\"hidden\" name=\"type\" value=\"%s\" />" checklistType) |> ignore
        for kvp in statuses do
            sb.AppendLine(sprintf "<input type=\"hidden\" name=\"status%d\" value=\"%s\" />" kvp.Key kvp.Value) |> ignore
        sb.AppendLine("<label>Name:<input type=\"text\" name=\"operatorName\" required /></label>") |> ignore
        sb.AppendLine(sprintf "<label>Date:<input type=\"date\" name=\"date\" value=\"%s\" required /></label>" (DateTime.Now.ToString("yyyy-MM-dd"))) |> ignore
        sb.AppendLine("<label>Final comment:<textarea name=\"finalComment\"></textarea></label>") |> ignore
        sb.AppendLine("<button type=\"submit\">Save to JSON</button>") |> ignore
        sb.AppendLine("</form>") |> ignore

        sb.AppendLine("<form><button type=\"button\" onclick=\"window.print()\">Print / Export</button></form>") |> ignore
        sb.AppendLine("<form method=\"get\" action=\"/\"><button type=\"submit\">Back to menu</button></form>") |> ignore
    else
        let taskText = list.[index]
        sb.AppendLine("<form method=\"post\" action=\"/step\">") |> ignore
        sb.AppendLine(sprintf "<p><b>%s</b></p>" taskText) |> ignore
        sb.AppendLine(sprintf "<input type=\"hidden\" name=\"type\" value=\"%s\" />" checklistType) |> ignore
        sb.AppendLine(sprintf "<input type=\"hidden\" name=\"index\" value=\"%d\" />" index) |> ignore
        for kvp in statuses do
            sb.AppendLine(sprintf "<input type=\"hidden\" name=\"status%d\" value=\"%s\" />" kvp.Key kvp.Value) |> ignore
        sb.AppendLine("<label>Status:<select name=\"currentStatus\">") |> ignore
        for opt in ["OK"; "FAIL"; "ERROR"; "NA"] do
            sb.AppendLine(sprintf "<option value=\"%s\">%s</option>" opt opt) |> ignore
        sb.AppendLine("</select></label>") |> ignore
        sb.AppendLine("<label>Comment:<textarea name=\"comment\"></textarea></label>") |> ignore
        sb.AppendLine("<button type=\"submit\" name=\"action\" value=\"back\">Back</button>") |> ignore
        sb.AppendLine("<button type=\"submit\" name=\"action\" value=\"next\">Next</button>") |> ignore
        sb.AppendLine("</form>") |> ignore

    sb.AppendLine("</div></body></html>") |> ignore
    sb.ToString()

let stepHandler (ctx: HttpContext) : Task =
    task {
        let! form = ctx.Request.ReadFormAsync()
        let t = form.["type"].ToString()
        let i = form.["index"].ToString() |> int
        let a = form.["action"].ToString()
        let cs = form.["currentStatus"].ToString()
        let comment = form.["comment"].ToString()
        let combined = cs + "|||" + comment

        let mutable statuses =
            form
            |> Seq.choose (fun kvp ->
                if kvp.Key.StartsWith("status") then
                    let idx = kvp.Key.Substring(6) |> int
                    Some (idx, kvp.Value.ToString())
                else None)
            |> Map.ofSeq
        statuses <- statuses.Add(i, combined)

        let len = getChecklist t |> List.length
        let newIndex =
            match a with
            | "next" when i < len -> i + 1
            | "back" when i > 0   -> i - 1
            | _ -> i

        ctx.Response.ContentType <- "text/html"
        do! ctx.Response.WriteAsync(renderStepPage t newIndex statuses comment)
    }

[<EntryPoint>]
let main _ =
    let builder = WebApplication.CreateBuilder()
    builder.Services.AddRouting() |> ignore
    let app = builder.Build()

    app.MapGet("/", fun (ctx: HttpContext) ->
        ctx.Response.ContentType <- "text/html"
        ctx.Response.WriteAsync(renderIndex())) |> ignore

    app.MapGet("/step", fun (ctx: HttpContext) ->
        let t = ctx.Request.Query.["type"].ToString()
        let i = ctx.Request.Query.["index"].ToString() |> int
        let statuses = Map.empty
        ctx.Response.ContentType <- "text/html"
        ctx.Response.WriteAsync(renderStepPage t i statuses "")) |> ignore

    app.MapPost("/step", RequestDelegate stepHandler) |> ignore

    app.MapPost("/save", RequestDelegate(fun ctx ->
        task {
            let! form = ctx.Request.ReadFormAsync()
            let checklistType = form.["type"].ToString()
            let operatorName = form.["operatorName"].ToString()
            let finalComment = form.["finalComment"].ToString()
            let date = form.["date"].ToString() |> DateTime.Parse

            let statuses =
                form
                |> Seq.choose (fun kvp ->
                    if kvp.Key.StartsWith("status") then
                        let idx = kvp.Key.Substring(6) |> int
                        Some (idx, kvp.Value.ToString())
                    else None)
                |> Map.ofSeq

            let result = {
                ChecklistType = checklistType
                OperatorName = operatorName
                FinalComment = finalComment
                Date = date
                Statuses = statuses
            }

            let json = JsonSerializer.Serialize(result, JsonSerializerOptions(WriteIndented = true))
            let timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss")
            let filename = $"results-{timestamp}.json"
            do! File.WriteAllTextAsync(filename, json)

            ctx.Response.ContentType <- "text/html"
            do! ctx.Response.WriteAsync("<html><body><h2>Checklist saved successfully.</h2><a href=\"/\">Back to menu</a></body></html>")
        })) |> ignore

    app.Run()
    0