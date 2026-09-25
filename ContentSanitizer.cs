using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace TrainingPlatform;

// ============================================================================
//  Saneado del contenido de los autores (bloque F2, hallazgo XSS-03)
//  -----------------------------------------------------------------
//  El reproductor pinta el HTML de los bloques de texto tal cual (innerHTML), así
//  que se sanea en el SERVIDOR al guardar un ítem (POST /trainings/{id}/items y
//  PUT /items/{id}) con HtmlSanitizer (Ganss.Xss) y una lista blanca de formato.
//  Los seeds de content/ pasan por esa misma API, así que quedan saneados al
//  resembrar.
//
//  Lo que se sanea del payload:
//    · HTML: blocks[].html (bloques de texto), bodyHtml (formato antiguo) y
//      description (pantalla de entrada 'intro').
//    · URLs: photo solo data:image/…;base64, /media/… o https:; mediaUrl solo
//      /media/… o https:. Lo demás se quita.
//    · photoPos (va a una variable CSS) y variant (va a una clase): solo valores
//      simples; lo demás se quita.
//  El resto de campos (title, subtitle, kicker, question, opciones y pareo) son
//  texto plano: el reproductor y el editor los escapan al pintarlos.
// ============================================================================
public static class ContentSanitizer
{
    // Formato permitido. Además de la lista del spec, el SVG simple que usan las
    // portadas de los cursos de content/ (ícono de línea con path y circle) y unas
    // pocas etiquetas de formato inofensivas que produce pegar desde otro documento.
    private static readonly string[] Etiquetas =
    {
        "p", "br", "b", "strong", "i", "em", "u", "ul", "ol", "li", "h2", "h3", "h4", "blockquote",
        "a", "img", "span", "div", "table", "thead", "tbody", "tr", "th", "td", "figure", "figcaption",
        "small", "sup", "sub",
        // extra, sin riesgo
        "h1", "h5", "h6", "hr", "s", "strike", "del", "ins", "mark", "code", "pre", "tfoot", "caption",
        "col", "colgroup", "abbr", "cite", "q", "dl", "dt", "dd",
        // SVG decorativo (sin script, sin enlaces, sin <use>, sin foreignObject)
        "svg", "path", "circle", "rect", "line", "polyline", "polygon", "ellipse", "g"
    };

    private static readonly string[] Atributos =
    {
        "class", "href", "target", "rel", "title", "src", "alt", "loading", "width", "height",
        "colspan", "rowspan", "align", "start", "type",
        // SVG (presentación y geometría)
        "viewbox", "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-dasharray",
        "stroke-opacity", "fill-opacity", "fill-rule", "opacity", "d", "cx", "cy", "r", "rx", "ry",
        "x", "y", "x1", "y1", "x2", "y2", "points", "transform", "preserveaspectratio", "xmlns"
    };

    // CSS de los atributos style. No se usa el saneado de CSS de HtmlSanitizer: pasa por
    // AngleSharp.Css, que reescribe los valores y descarta los que no entiende (flex:none,
    // por ejemplo, desaparecía y descuadraba los pasos numerados de los cursos). Aquí cada
    // declaración se conserva tal cual si la propiedad está en la lista y el valor no trae
    // nada que cargue recursos o ejecute código (url(), expression, escapes…).
    private static readonly HashSet<string> Css = new(StringComparer.OrdinalIgnoreCase)
    {
        "color", "background", "background-color", "background-position", "background-size", "background-repeat",
        "border", "border-top", "border-right", "border-bottom", "border-left", "border-color", "border-style",
        "border-width", "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
        "border-top-style", "border-right-style", "border-bottom-style", "border-left-style", "border-top-width",
        "border-right-width", "border-bottom-width", "border-left-width", "border-radius", "border-top-left-radius",
        "border-top-right-radius", "border-bottom-left-radius", "border-bottom-right-radius", "border-collapse",
        "border-spacing", "outline", "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
        "margin", "margin-top", "margin-right", "margin-bottom", "margin-left", "font", "font-size", "font-weight",
        "font-style", "font-family", "font-variant", "line-height", "letter-spacing", "word-spacing", "text-align",
        "text-decoration", "text-transform", "text-indent", "text-shadow", "text-overflow", "white-space",
        "word-break", "overflow-wrap", "vertical-align", "display", "visibility", "width", "height", "min-width",
        "min-height", "max-width", "max-height", "box-sizing", "box-shadow", "opacity", "overflow", "overflow-x",
        "overflow-y", "float", "clear", "list-style", "list-style-type", "list-style-position", "gap", "row-gap",
        "column-gap", "flex", "flex-grow", "flex-shrink", "flex-basis", "flex-wrap", "flex-direction", "flex-flow",
        "align-items", "align-self", "align-content", "justify-content", "justify-items", "justify-self",
        "place-items", "place-content", "order", "grid-template-columns", "grid-template-rows", "grid-column",
        "grid-row", "grid-gap", "grid-auto-flow", "object-fit", "object-position", "aspect-ratio", "position",
        "top", "right", "bottom", "left", "inset", "table-layout", "caption-side", "fill", "stroke", "stroke-width"
    };
    private static readonly Regex CssValorProhibido = new(
        @"url\s*\(|image(-set)?\s*\(|element\s*\(|expression|javascript:|vbscript:|behavior|binding|[\\<>{}@]|/\*|\*/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Elementos cuyo contenido tampoco se conserva al quitarlos (el resto de etiquetas
    // no permitidas se quitan dejando su texto, para no perder contenido visible).
    private static readonly HashSet<string> SinContenido = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "frame", "frameset", "object", "embed", "applet", "noscript",
        "noembed", "noframes", "template", "textarea", "select", "xmp", "plaintext", "math", "title",
        "head", "meta", "link", "base", "form"
    };

    public static readonly Regex DataImage =
        new(@"^data:image/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/\s]+={0,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhotoPos = new(@"^[\w%.\s-]{0,40}$", RegexOptions.Compiled);
    private static readonly string[] Variantes = { "left", "right", "lines" };

    private static readonly HtmlSanitizer Html = Crear();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IElement, string> Estilos = new();

    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static HtmlSanitizer Crear()
    {
        var s = new HtmlSanitizer { KeepChildNodes = true, AllowDataAttributes = false };
        s.AllowedTags.Clear();
        foreach (var t in Etiquetas) s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        foreach (var a in Atributos) s.AllowedAttributes.Add(a);
        s.AllowedAtRules.Clear();
        // Las URL de href/src las valida PostProcessNode con reglas por etiqueta (un data:
        // de cientos de KB no cabe en System.Uri, y HtmlSanitizer lo descartaría).
        s.UriAttributes.Clear();
        s.AllowedSchemes.Clear();

        // Un <script>, <style>, <iframe>… se quita entero, sin dejar su texto.
        s.RemovingTag += (_, e) =>
        {
            if (SinContenido.Contains(e.Tag.LocalName))
                while (e.Tag.FirstChild is { } n) e.Tag.RemoveChild(n);
        };
        // style no está en AllowedAttributes: HtmlSanitizer lo quita y aquí se guarda la
        // versión saneada, que PostProcessNode vuelve a poner al final.
        s.RemovingAttribute += (_, e) =>
        {
            if (e.Reason == RemoveReason.NotAllowedAttribute && e.Attribute.Name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var limpio = SanitizeStyle(e.Attribute.Value);
                if (limpio.Length > 0) Estilos.AddOrUpdate(e.Tag, limpio);
            }
        };
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement el) return;
            if (Estilos.TryGetValue(el, out var estilo)) { el.SetAttribute("style", estilo); Estilos.Remove(el); }
            Urls(el);
        };
        return s;
    }

    // Reglas de URL por etiqueta: <a href> solo http, https o mailto (y target/rel
    // seguros); <img src> solo data:image/…;base64, /media/… o https:. Ninguna otra
    // etiqueta lleva href ni src.
    private static void Urls(IElement el)
    {
        var tag = el.LocalName;
        var href = el.GetAttribute("href");
        if (href is not null)
        {
            var h = href.Trim();
            if (tag == "a" && Regex.IsMatch(h, @"^(https?://|mailto:)\S", RegexOptions.IgnoreCase)) el.SetAttribute("href", h);
            else el.RemoveAttribute("href");
        }
        var src = el.GetAttribute("src");
        if (src is not null)
        {
            if (tag == "img" && ImagenSegura(src)) el.SetAttribute("src", src.Trim());
            else el.RemoveAttribute("src");
        }
        if (tag == "a")
        {
            var target = el.GetAttribute("target");
            if (target is not null && target != "_blank") el.RemoveAttribute("target");
            if (el.GetAttribute("target") == "_blank")
            {
                var rel = (el.GetAttribute("rel") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!rel.Contains("noopener")) rel.Add("noopener");
                el.SetAttribute("rel", string.Join(' ', rel));
            }
        }
        else { el.RemoveAttribute("target"); el.RemoveAttribute("rel"); }
    }

    // src de imagen permitido: data:image/(png|jpeg|gif|webp);base64, /media/… o https://
    public static bool ImagenSegura(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)) return false;
        var s = src.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return DataImage.IsMatch(s);
        return MedioSeguro(s);
    }

    // URL de medio permitida: /media/… del propio servidor o https:// absoluta.
    public static bool MedioSeguro(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var s = url.Trim();
        if (s.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)) return !s.Contains('\\');
        return s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               && Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
    }

    // Deja solo las declaraciones "propiedad: valor" permitidas, con el valor tal cual
    // (espacios y saltos de línea colapsados). position: fixed/sticky no se permite.
    public static string SanitizeStyle(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return "";
        var fuera = new List<string>();
        foreach (var decl in Declaraciones(style))
        {
            var i = decl.IndexOf(':');
            if (i <= 0) continue;
            var prop = decl[..i].Trim().ToLowerInvariant();
            var valor = Regex.Replace(decl[(i + 1)..], @"\s+", " ").Trim();
            if (!Css.Contains(prop) || valor.Length == 0 || valor.Length > 500 || CssValorProhibido.IsMatch(valor)) continue;
            if (prop == "position" && Regex.IsMatch(valor, "fixed|sticky", RegexOptions.IgnoreCase)) continue;
            fuera.Add(prop + ":" + valor);
        }
        return string.Join(";", fuera);
    }

    // Parte un style por ';' fuera de paréntesis y comillas (rgba(…), "Segoe UI").
    private static IEnumerable<string> Declaraciones(string style)
    {
        int nivel = 0, ini = 0; char? comilla = null;
        for (int i = 0; i < style.Length; i++)
        {
            var c = style[i];
            if (comilla is not null) { if (c == comilla) comilla = null; continue; }
            if (c is '"' or '\'') comilla = c;
            else if (c == '(') nivel++;
            else if (c == ')' && nivel > 0) nivel--;
            else if (c == ';' && nivel == 0) { yield return style[ini..i]; ini = i + 1; }
        }
        if (ini < style.Length) yield return style[ini..];
    }

    public static string SanitizeHtml(string? html) => string.IsNullOrEmpty(html) ? "" : Html.Sanitize(html);

    // Sanea el PayloadJson de un ítem. Devuelve null si no es un objeto JSON válido
    // (el endpoint responde 400). Si nada cambió, devuelve el texto original tal cual.
    public static string? SanitizePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        JsonObject? p;
        try { p = JsonNode.Parse(payloadJson) as JsonObject; }
        catch (JsonException) { return null; }
        if (p is null) return null;

        var cambio = false;
        cambio |= HtmlCampo(p, "bodyHtml");
        cambio |= HtmlCampo(p, "description");
        cambio |= UrlCampo(p, "photo", ImagenSegura);
        cambio |= UrlCampo(p, "mediaUrl", MedioSeguro);
        if (p["photoPos"] is JsonNode pp && !(pp is JsonValue v1 && v1.TryGetValue<string>(out var ps) && PhotoPos.IsMatch(ps)))
        { p.Remove("photoPos"); cambio = true; }
        if (p["variant"] is JsonNode vn && !(vn is JsonValue v2 && v2.TryGetValue<string>(out var vs) && Variantes.Contains(vs)))
        { p.Remove("variant"); cambio = true; }
        if (p["blocks"] is JsonArray blocks)
            foreach (var b in blocks.OfType<JsonObject>())
            {
                cambio |= HtmlCampo(b, "html");
                cambio |= UrlCampo(b, "mediaUrl", MedioSeguro);
            }
        return cambio ? p.ToJsonString(Json) : payloadJson;
    }

    private static bool HtmlCampo(JsonObject o, string campo)
    {
        if (o[campo] is not JsonNode n) return false;
        if (n is not JsonValue v || !v.TryGetValue<string>(out var html)) { o.Remove(campo); return true; }
        var limpio = SanitizeHtml(html);
        if (limpio == html) return false;
        o[campo] = limpio;
        return true;
    }

    private static bool UrlCampo(JsonObject o, string campo, Func<string?, bool> valida)
    {
        if (o[campo] is not JsonNode n) return false;
        if (n is JsonValue v && v.TryGetValue<string>(out var url) && (url.Length == 0 || valida(url))) return false;
        o.Remove(campo);
        return true;
    }
}
