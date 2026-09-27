using System.Net;
using System.Net.Http.Headers;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.Multitenancy;
using TrainingPlatform.TenantData;

namespace TrainingPlatform;

// ============================================================================
// Voz de las láminas. El texto de cada ítem se graba UNA vez con Azure AI Speech (voces
// neuronales de Puerto Rico) y el MP3 se guarda como un archivo más del curso (MediaAsset,
// igual que las fotos subidas). El ítem lo enlaza en su payload:
//   narration: { url: "/media/{id}", voice: "es-PR-KarinaNeural", hash: "…" }
// `hash` es la huella de voz + texto: si alguien cambia el texto de la lámina, el audio
// viejo deja de valer (Narracion.QuitarSiVencida al guardar el ítem) y la siguiente
// grabación lo rehace. Al crear un borrador desde la versión publicada, el payload se copia
// con su narración: solo se regraba lo que cambió.
//
// Clave y región del recurso de Azure: Speech:Key y Speech:Region (en producción,
// APRENDOR_Speech__Key y APRENDOR_Speech__Region). Sin clave la función queda apagada: la
// ficha del curso lo dice y el reproductor no muestra el botón «Escuchar».
// ============================================================================
public static class Narracion
{
    public static readonly string[] Voces = { "es-PR-KarinaNeural", "es-PR-VictorNeural" };
    public const string VozPorDefecto = "es-PR-KarinaNeural";

    // Una lámina normal son 300–900 caracteres; el tope evita mandar a Azure algo absurdo.
    private const int MaxCaracteres = 5000;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly Regex Region = new("^[a-z0-9]{3,30}$", RegexOptions.Compiled);
    private static readonly Regex UrlMedia = new("^/media/[0-9a-fA-F-]{36}$", RegexOptions.Compiled);
    private static readonly Regex Hex = new("^[0-9a-f]{16,64}$", RegexOptions.Compiled);
    private static readonly Regex FinBloque = new(@"<br\s*/?>|</(p|li|h[1-6]|div|tr)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Etiqueta = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);

    public static bool Disponible(IConfiguration cfg)
        => !string.IsNullOrWhiteSpace(cfg["Speech:Key"]) && Region.IsMatch((cfg["Speech:Region"] ?? "").Trim().ToLowerInvariant());

    // ---- Texto que se lee ----
    // Info: título y cuerpo (cada párrafo o viñeta es una frase); la portada, solo el título;
    // la pantalla de entrada no se lee. Módulo: título y subtítulo. Pregunta: el enunciado y
    // las opciones con su letra, como las pinta el reproductor (a., b., c.…).
    public static string Texto(string type, JsonObject p)
    {
        var frases = new List<string>();
        void Add(string? s) { if (!string.IsNullOrWhiteSpace(s)) frases.Add(s.Trim()); }
        var str = (string k) => p[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        switch (type)
        {
            case "Info":
            {
                var layout = str("layout");
                if (layout == "intro") return "";
                Add(str("title"));
                if (layout == "cover") break;
                if (p["blocks"] is JsonArray blocks)
                    foreach (var b in blocks.OfType<JsonObject>())
                        if (b["html"] is JsonValue hv && hv.TryGetValue<string>(out var html)) frases.AddRange(Frases(html));
                if (str("bodyHtml") is string body) frases.AddRange(Frases(body));
                break;
            }
            case "ModuleHeader":
                Add(str("title") is string t0 ? Regex.Replace(t0, @"\s*[—–]\s*", ": ") : null);
                Add(str("subtitle"));
                break;
            case "MultipleChoice":
            case "MultiSelect":
            {
                Add(str("question"));
                var ops = (p["options"] as JsonArray)?.OfType<JsonObject>()
                    .Select(o => o["text"] is JsonValue t && t.TryGetValue<string>(out var s) ? s.Trim() : "").ToList() ?? new();
                var ciertoFalso = ops.Count == 2 && ops.All(o => Regex.IsMatch(o, "^(cierto|verdadero|falso)$", RegexOptions.IgnoreCase));
                if (ciertoFalso) Add("¿Cierto o falso?");
                else for (int i = 0; i < ops.Count; i++) Add($"{(char)('a' + i)}: {ops[i]}");
                if (type == "MultiSelect") Add("Puede haber más de una respuesta correcta.");
                break;
            }
            case "Matching":
                Add(str("question"));
                if (p["prompts"] is JsonArray prompts)
                    foreach (var pr in prompts.OfType<JsonObject>())
                        if (pr["text"] is JsonValue t && t.TryGetValue<string>(out var s)) Add(s);
                break;
            case "OpenResponse":
                Add(str("question"));
                break;
        }
        // Una frase igual a la anterior no se repite: las láminas con el título también como
        // subtítulo en el cuerpo (dark con heading(), p. ej. «Objetivo») se ven una sola vez
        // en pantalla y así se oyen una sola vez.
        static string Norm(string s) => Espacios.Replace(s, " ").Trim().TrimEnd('.', ':', ';', '!', '?', '…').Trim().ToLowerInvariant();
        frases = frases.Where((f, i) => i == 0 || Norm(f) != Norm(frases[i - 1])).ToList();
        var texto = string.Join(" ", frases.Select(Punto));
        return texto.Length > MaxCaracteres ? texto[..MaxCaracteres] : texto;
    }

    // Los saltos de línea del HTML no separan frases (solo los fines de párrafo, viñeta o
    // <br>); las etiquetas en línea (<b>, <a>…) se quitan sin dejar espacio de más.
    private static IEnumerable<string> Frases(string html)
        => FinBloque.Replace(Espacios.Replace(html, " "), "\n").Split('\n')
            .Select(s => Espacios.Replace(WebUtility.HtmlDecode(Etiqueta.Replace(s, "")), " ").Trim())
            .Where(s => s.Length > 0);

    // Cada frase termina en puntuación, para que la voz haga la pausa entre viñetas.
    private static string Punto(string s) => ".!?:;…".Contains(s[^1]) ? s : s + ".";

    public static string Hash(string voz, string texto)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(voz + "\n" + texto)))[..32].ToLowerInvariant();

    // La narración del payload corresponde al texto actual (y es una de las voces conocidas).
    public static bool Vigente(string type, JsonObject p, string? voz = null)
    {
        if (p["narration"] is not JsonObject n) return false;
        if (n["voice"] is not JsonValue vv || !vv.TryGetValue<string>(out var v)) return false;
        if (!Voces.Contains(v) || (voz is not null && v != voz)) return false;
        if (n["hash"] is not JsonValue hv || !hv.TryGetValue<string>(out var h)) return false;
        var texto = Texto(type, p);
        return texto.Length > 0 && h == Hash(v, texto);
    }

    // Al guardar un ítem: si el texto cambió, la narración vieja se quita (su audio ya no
    // corresponde) y queda pendiente de grabar otra vez.
    public static string QuitarSiVencida(string type, string payloadJson)
    {
        JsonObject? p;
        try { p = JsonNode.Parse(payloadJson) as JsonObject; } catch { return payloadJson; }
        if (p is null || p["narration"] is null) return payloadJson;
        bool vigente;
        try { vigente = Vigente(type, p); } catch { vigente = false; }
        if (vigente) return payloadJson;
        p.Remove("narration");
        return p.ToJsonString();
    }

    // Saneado (ContentSanitizer): narration solo puede ser { url /media/{guid}, voz conocida,
    // hash hexadecimal }. Cualquier otra cosa se quita: el reproductor la usa como src de <audio>.
    public static bool Sanear(JsonObject p)
    {
        if (p["narration"] is not JsonNode n) return false;
        try
        {
            if (n is JsonObject o
                && o["url"] is JsonValue u && u.TryGetValue<string>(out var url) && UrlMedia.IsMatch(url)
                && o["voice"] is JsonValue vv && vv.TryGetValue<string>(out var voz) && Voces.Contains(voz)
                && o["hash"] is JsonValue hv && hv.TryGetValue<string>(out var h) && Hex.IsMatch(h)
                && o.Count == 3)
                return false;
        }
        catch { }
        p.Remove("narration");
        return true;
    }

    // Consola (dotnet TrainingPlatform.dll voz-texto course.json): el texto de cada ítem
    // activo y el total de caracteres, para revisar la lectura y estimar el consumo.
    public static void Consola(string ruta)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var curso = JsonNode.Parse(File.ReadAllText(ruta)) as JsonObject;
        var items = curso?["items"] as JsonArray ?? new JsonArray();
        int n = 0, total = 0;
        foreach (var it in items.OfType<JsonObject>())
        {
            if (it["active"] is JsonValue a && a.TryGetValue<bool>(out var activo) && !activo) continue;
            var type = it["type"]?.GetValue<string>() ?? "";
            if (it["payload"] is not JsonObject p) continue;
            var texto = Texto(type, p);
            if (texto.Length == 0) continue;
            n++; total += texto.Length;
            Console.WriteLine($"{n,3}. [{type}] {texto.Length} car.  {texto}");
        }
        Console.WriteLine();
        Console.WriteLine($"{n} láminas con voz, {total:N0} caracteres.");
    }

    // ---- Azure ----
    public sealed class Ocupado(TimeSpan espera) : Exception("Azure pidió esperar.") { public TimeSpan Espera { get; } = espera; }

    public static async Task<byte[]> GrabarAsync(IConfiguration cfg, string voz, string texto, CancellationToken ct)
    {
        var region = cfg["Speech:Region"]!.Trim().ToLowerInvariant();
        var ssml = $"<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"es-PR\">" +
                   $"<voice name=\"{voz}\">{SecurityElement.Escape(texto)}</voice></speak>";
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1");
        req.Headers.Add("Ocp-Apim-Subscription-Key", cfg["Speech:Key"]!.Trim());
        req.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        req.Headers.UserAgent.ParseAdd("Aprendor");
        req.Content = new StringContent(ssml, Encoding.UTF8);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/ssml+xml");
        using var res = await Http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.TooManyRequests)
            throw new Ocupado(res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10));
        if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Azure rechazó la clave de voz (revisa Speech:Key y Speech:Region en el servidor).");
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure no pudo grabar la voz (HTTP {(int)res.StatusCode}).");
        var audio = await res.Content.ReadAsByteArrayAsync(ct);
        if (audio.Length < 200) throw new InvalidOperationException("Azure devolvió un audio vacío.");
        return audio;
    }

    // ---- Endpoints ----
    private static bool CanAuthor(string? role) => role is "Admin" or "Author";

    // La versión a la que se le graba la voz: el borrador si lo hay; si no, la última
    // publicada. La narración no cambia el contenido (solo añade el audio del mismo texto),
    // así que se puede añadir a una versión publicada sin crear un borrador nuevo.
    private static Task<TrainingVersion?> VersionAsync(TenantDbContext db, Guid trainingId)
        => db.TrainingVersions.Where(v => v.TrainingId == trainingId && (v.Status == "draft" || v.Status == "published"))
            .OrderByDescending(v => v.Status == "draft").ThenByDescending(v => v.VersionNumber).FirstOrDefaultAsync();

    private sealed record Estado(int Total, int Hechas, string? Voz);

    private static Estado Contar(IEnumerable<TrainingItem> items)
    {
        int total = 0, hechas = 0; var voces = new Dictionary<string, int>();
        foreach (var it in items)
        {
            JsonObject? p; try { p = JsonNode.Parse(it.PayloadJson) as JsonObject; } catch { continue; }
            if (p is null || Texto(it.Type, p).Length == 0) continue;
            total++;
            if (Vigente(it.Type, p))
            {
                hechas++;
                if (p["narration"]!["voice"] is JsonValue vv && vv.TryGetValue<string>(out var v))
                    voces[v] = voces.GetValueOrDefault(v) + 1;
            }
        }
        return new Estado(total, hechas, voces.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault());
    }

    public static void MapNarracion(this WebApplication app)
    {
        // Estado: si hay clave de Azure, cuántas láminas de la versión tienen voz y con qué voz.
        app.MapGet("/trainings/{id:guid}/narration", async (Guid id, ITenantContext tc, IServiceProvider sp, IConfiguration cfg) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            var db = sp.GetRequiredService<TenantDbContext>();
            var version = await VersionAsync(db, id);
            if (version is null) return Results.NotFound();
            var items = await db.TrainingItems.AsNoTracking().Where(i => i.TrainingVersionId == version.Id && i.Active).ToListAsync();
            var e = Contar(items);
            return Results.Ok(new
            {
                available = Disponible(cfg), voices = Voces, voice = e.Voz ?? VozPorDefecto,
                versionId = version.Id, versionStatus = version.Status, total = e.Total, done = e.Hechas
            });
        }).RequireAuthorization();

        // Graba hasta `max` láminas pendientes (sin voz, con otra voz o con el texto cambiado)
        // y responde cuántas faltan: la ficha del curso y los scripts de carga lo llaman en
        // bucle hasta que no quede ninguna. Si Azure pide esperar (nivel gratis), responde
        // `wait` en segundos y quien llama espera antes de seguir.
        app.MapPost("/trainings/{id:guid}/narration", async (Guid id, NarrationRequest? req, ITenantContext tc, IServiceProvider sp,
            IConfiguration cfg, IWebHostEnvironment env, HttpContext http, ILoggerFactory logs) =>
        {
            if (tc.TenantId is null) return Results.BadRequest("No tenant context.");
            if (!CanAuthor(tc.Role)) return Results.Forbid();
            if (!Disponible(cfg))
                return Results.BadRequest("La voz no está configurada en el servidor (falta la clave de Azure Speech).");
            var voz = req?.Voice ?? VozPorDefecto;
            if (!Voces.Contains(voz)) return Results.BadRequest("Voz desconocida.");
            var max = Math.Clamp(req?.Max ?? 8, 1, 25);

            var db = sp.GetRequiredService<TenantDbContext>();
            var version = await VersionAsync(db, id);
            if (version is null) return Results.NotFound();
            var items = await db.TrainingItems.Where(i => i.TrainingVersionId == version.Id && i.Active)
                .OrderBy(i => i.Order).ToListAsync();

            var pendientes = new List<(TrainingItem It, JsonObject P, string Texto)>();
            foreach (var it in items)
            {
                JsonObject? p; try { p = JsonNode.Parse(it.PayloadJson) as JsonObject; } catch { continue; }
                if (p is null) continue;
                var texto = Texto(it.Type, p);
                if (texto.Length == 0 || Vigente(it.Type, p, voz)) continue;
                pendientes.Add((it, p, texto));
            }

            var root = Path.Combine(env.ContentRootPath, cfg["Storage:UploadsPath"] ?? "App_Data/uploads");
            Directory.CreateDirectory(Path.Combine(root, tc.TenantId.Value.ToString()));
            var log = logs.CreateLogger("Narracion");
            int grabadas = 0, caracteres = 0; double? espera = null; string? error = null;
            foreach (var (it, p, texto) in pendientes.Take(max))
            {
                byte[] audio;
                try { audio = await GrabarAsync(cfg, voz, texto, http.RequestAborted); }
                catch (Ocupado o) { espera = Math.Ceiling(o.Espera.TotalSeconds); break; }
                catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
                {
                    log.LogWarning(ex, "No se pudo grabar la voz del ítem {Item}", it.Id);
                    error = ex is InvalidOperationException ? ex.Message : "No se pudo conectar con Azure para grabar la voz.";
                    break;
                }
                var mid = Guid.NewGuid();
                var rel = Path.Combine(tc.TenantId.Value.ToString(), mid + ".mp3");
                await File.WriteAllBytesAsync(Path.Combine(root, rel), audio);
                db.MediaAssets.Add(new MediaAsset
                {
                    Id = mid, FileName = $"voz-{it.Id}.mp3", ContentType = "audio/mpeg",
                    RelativePath = rel, Size = audio.Length, Purpose = "course"
                });
                p["narration"] = new JsonObject { ["url"] = $"/media/{mid}", ["voice"] = voz, ["hash"] = Hash(voz, texto) };
                it.PayloadJson = p.ToJsonString();
                await db.SaveChangesAsync();
                grabadas++; caracteres += texto.Length;
            }
            if (grabadas > 0)
                log.LogInformation("Voz grabada: {N} láminas, {C} caracteres, curso {Curso}", grabadas, caracteres, id);

            var e = Contar(items);
            var faltan = pendientes.Count - grabadas;   // con la voz pedida
            if (error is not null && grabadas == 0) return Results.Json(new { error }, statusCode: StatusCodes.Status502BadGateway);
            return Results.Ok(new { recorded = grabadas, total = e.Total, done = e.Total - faltan, pending = faltan, wait = espera, error });
        }).RequireAuthorization();
    }
}

public record NarrationRequest(string? Voice, int? Max);
