using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TrainingPlatform.Certificates;

// Generador de PDF del certificado, escrito a mano.
//
// Por qué a mano y no con una librería: el PDF va adjunto en un correo que sale
// solo, en servidores que no controlamos (IIS en casa del cliente, contenedor
// Linux, lo que toque). Las librerías de PDF arrastran fuentes y dependencias
// nativas que fallan distinto en cada sitio. Aquí se usan las fuentes estándar
// del propio formato (Times y Helvetica), que todo lector trae: cero
// archivos, cero dependencias, mismo resultado en cualquier lado.
//
// El diseño es el MISMO de la plantilla en pantalla (wwwroot/certificate.html): doble
// marco en el color de acento, emisor, «Certificado de aprobación», «Constancia de
// cumplimiento», nombre en cursiva con línea, curso, leyenda, fila de fecha /
// calificación / vigencia, firma a la izquierda, sello circular a la derecha y folio al
// pie. Se maqueta en «px» de la hoja de 1000 px de ancho de esa plantilla y se escala a
// A4 apaisado (la misma proporción 1.414 y el mismo @page de certificate.html). Si se
// cambia la plantilla, hay que cambiar esto también. Georgia pasa a Times (serif) y
// system-ui a Helvetica.
//
// Logo y firma: se incrustan si son PNG (no entrelazado) o JPEG, que el PDF entiende
// casi tal cual. WebP y GIF no: el certificado sale igual, sin la imagen.
public static class CertificatePdf
{
    // ---- Página: A4 apaisado, en puntos; S convierte px de la plantilla a puntos ----
    private const double AnchoPag = 841.89, AltoPag = 595.28;
    private const double S = AnchoPag / 1000.0;
    private const double AltoPx = AltoPag / S;          // ~707 px, como la hoja en pantalla

    // Fuentes estándar (nombre de recurso en la página).
    private enum Fuente { Serif, SerifNegrita, SerifCursiva, Sans, SansNegrita }

    private static readonly string[] NombresFuente =
        { "Times-Roman", "Times-Bold", "Times-Italic", "Helvetica", "Helvetica-Bold" };

    // Georgia es más ancha que Times (medido en Chrome: 1.10 normal, 1.21 negrita, 1.11
    // cursiva). Los cortes de línea y los encogimientos se deciden con el ancho que
    // tendría en Georgia, para que el PDF parta las líneas donde las parte la pantalla.
    private static double FactorGeorgia(Fuente f)
        => f switch { Fuente.Serif => 1.10, Fuente.SerifNegrita => 1.21, Fuente.SerifCursiva => 1.11, _ => 1.0 };

    // Anchos en milésimas del cuerpo, WinAnsi 32..255 (de los AFM de Adobe).
    private static readonly int[][] Anchos =
    {
        new[] { // Times-Roman
        250,333,408,500,500,833,778,180,333,333,500,564,250,333,250,278,500,500,500,500,500,500,500,500,500,500,278,278,
        564,564,564,444,921,722,667,667,722,611,556,722,722,333,389,722,611,889,722,722,556,722,667,556,611,722,722,944,
        722,722,611,333,278,333,469,500,333,444,500,444,500,444,333,500,500,278,278,500,278,778,500,500,500,500,333,389,
        278,500,500,722,500,500,444,480,200,480,541,0,500,0,333,500,444,1000,500,500,333,1000,556,333,889,0,611,0,
        0,333,333,444,444,350,500,1000,333,980,389,333,722,0,444,722,250,333,500,500,500,500,200,500,333,760,276,500,
        564,333,760,333,400,564,300,300,333,500,453,250,333,300,310,500,750,750,750,444,722,722,722,722,722,722,889,667,
        611,611,611,611,333,333,333,333,722,722,722,722,722,722,722,564,722,722,722,722,722,722,556,500,444,444,444,444,
        444,444,667,444,444,444,444,444,278,278,278,278,500,500,500,500,500,500,500,564,500,500,500,500,500,500,500,500 },
        new[] { // Times-Bold
        250,333,555,500,500,1000,833,278,333,333,500,570,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,
        570,570,570,500,930,722,667,722,722,667,611,778,778,389,500,778,667,944,722,778,611,778,722,556,667,722,722,1000,
        722,722,667,333,278,333,581,500,333,500,556,444,556,444,333,500,556,278,333,556,278,833,556,500,556,556,444,389,
        333,556,500,722,500,500,444,394,220,394,520,0,500,0,333,500,500,1000,500,500,333,1000,556,333,1000,0,667,0,
        0,333,333,500,500,350,500,1000,333,1000,389,333,722,0,444,722,250,333,500,500,500,500,220,500,333,747,300,500,
        570,333,747,333,400,570,300,300,333,556,540,250,333,300,330,500,750,750,750,500,722,722,722,722,722,722,1000,722,
        667,667,667,667,389,389,389,389,722,722,778,778,778,778,778,570,778,722,722,722,722,722,611,556,500,500,500,500,
        500,500,722,444,444,444,444,444,278,278,278,278,500,556,500,500,500,500,500,570,500,556,556,556,556,500,556,500 },
        new[] { // Times-Italic
        250,333,420,500,500,833,778,214,333,333,500,675,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,
        675,675,675,500,920,611,611,667,722,611,611,722,722,333,444,667,556,833,667,722,611,722,611,500,556,722,611,833,
        611,556,556,389,278,389,422,500,333,500,500,444,500,444,278,500,500,278,278,444,278,722,500,500,500,500,389,389,
        278,500,444,667,444,444,389,400,275,400,541,0,500,0,333,500,556,889,500,500,333,1000,500,333,944,0,556,0,
        0,333,333,556,556,350,500,889,333,980,389,333,667,0,389,556,250,389,500,500,500,500,275,500,333,760,276,500,
        675,333,760,333,400,675,300,300,333,500,523,250,333,300,310,500,750,750,750,500,611,611,611,611,611,611,889,667,
        611,611,611,611,333,333,333,333,722,667,722,722,722,722,722,675,722,722,722,722,722,556,611,500,500,500,500,500,
        500,500,667,444,444,444,444,444,278,278,278,278,500,500,500,500,500,500,500,675,500,500,500,500,500,444,500,444 },
        new[] { // Helvetica
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,
        584,584,584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,
        667,667,611,278,278,278,469,556,333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,
        278,556,500,722,500,500,500,334,260,334,584,0,556,0,222,556,333,1000,556,556,333,1000,667,333,1000,0,611,0,
        0,222,222,333,333,350,556,1000,333,1000,500,333,944,0,500,667,278,333,556,556,556,556,260,556,333,737,370,556,
        584,333,737,333,400,584,333,333,333,556,537,278,333,333,365,556,834,834,834,611,667,667,667,667,667,667,1000,722,
        667,667,667,667,278,278,278,278,722,722,778,778,778,778,778,584,778,722,722,722,722,667,667,611,556,556,556,556,
        556,556,889,500,556,556,556,556,278,278,278,278,556,556,556,556,556,556,556,584,611,556,556,556,556,500,556,500 },
        new[] { // Helvetica-Bold
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,
        584,584,584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,
        667,667,611,333,278,333,584,556,333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,
        333,611,556,778,556,556,500,389,280,389,584,0,556,0,278,556,500,1000,556,556,333,1000,667,333,1000,0,611,0,
        0,278,278,500,500,350,556,1000,333,1000,556,333,944,0,500,667,278,333,556,556,556,556,280,556,333,737,370,556,
        584,333,737,333,400,584,333,333,333,611,556,278,333,333,365,556,834,834,834,611,722,722,722,722,722,722,1000,722,
        667,667,667,667,278,278,278,278,722,722,778,778,778,778,778,584,778,722,722,722,722,667,667,611,556,556,556,556,
        556,556,889,556,556,556,556,556,278,278,278,278,611,611,611,611,611,611,611,584,611,611,611,611,611,556,611,556 },
    };

    // Proporciones verticales de las fuentes de la plantilla con line-height normal:
    // Georgia (alto de línea 1.136, línea base a 0.917) y Segoe UI / system-ui (1.33 y 1.079).
    private const double SerifLinea = 1.136, SerifBase = 0.917, SansLinea = 1.33, SansBase = 1.079;

    // Colores de la plantilla (tailwind gray-*).
    private static readonly (double r, double g, double b)
        Tinta = Hex("#1f2937"), Gris700 = Hex("#374151"), Gris600 = Hex("#4b5563"), Gris500 = Hex("#6b7280"),
        Gris400 = Hex("#9ca3af"), Gris300 = Hex("#d1d5db"), Plataforma = Hex("#b0b7c3"),
        Vigente = Hex("#047857"), Vencido = Hex("#b45309"), Anulado = Hex("#b91c1c");

    private static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    // Misma fecha que certificate.html (toLocaleDateString 'es', día 2 dígitos y mes largo).
    private static string Fecha(DateTime utc)
    {
        var f = utc.ToLocalTime();
        return $"{f.Day:00} de {Meses[f.Month - 1]} de {f.Year}";
    }

    public static byte[] Render(
        string issuerName, string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, int scorePercent, int passPercent,
        bool showScore, bool showValidity, string? statement,
        string? signatoryName, string? signatoryTitle, string accentColor,
        string? logoDataUrl = null, string? signatureDataUrl = null,
        string? estado = null, DateTime? estadoFecha = null, string? estadoMotivo = null)
    {
        var acento = Color(accentColor);
        var p = new Lienzo();
        // «Pedir que lo repita»: un certificado anulado lleva la marca de agua ANULADO (detrás
        // de todo) y la fecha y el motivo arriba; uno reemplazado, una nota en gris.
        bool anulado = estado == "voided", reemplazado = estado == "superseded";
        if (anulado) p.MarcaAgua("ANULADO", 500, AltoPx / 2, Fuente.SansNegrita, 150, Mezcla(Anulado, 0.16), 26);

        // --- Doble marco (.frame: 3px a 18px del borde; ::after: 1px a 28px, opacidad .5) ---
        p.Rect(19.5, 19.5, 1000 - 39, AltoPx - 39, 3, acento);
        p.Rect(28.5, 28.5, 1000 - 57, AltoPx - 57, 1, Mezcla(acento, 0.5));

        // Nota de estado entre el marco y el cuerpo (11px, centrada, recortada si no cabe).
        if (anulado || reemplazado)
        {
            var nota = anulado
                ? "ANULADO" + (estadoFecha is DateTime fa ? " el " + Fecha(fa) : "") +
                  (string.IsNullOrWhiteSpace(estadoMotivo) ? "" : " · Motivo: " + estadoMotivo!.Trim())
                : "Reemplazado por el certificado del " + (estadoFecha is DateTime fr ? Fecha(fr) : "—") +
                  (string.IsNullOrWhiteSpace(estadoMotivo) ? "" : " (folio " + estadoMotivo!.Trim() + ")");
            var f = anulado ? Fuente.SansNegrita : Fuente.Sans;
            while (nota.Length > 12 && Lienzo.Ancho(nota, f, 11) > 860) nota = nota[..^2].TrimEnd() + "…";
            p.Texto(nota, 500, 52, f, 11, anulado ? Anulado : Gris500);
        }

        // --- Cuerpo centrado (.inner: contenido de x 84 a 916, desde y 70) ---
        const double Centro = 500, AnchoCuerpo = 832;
        double y = 70;

        var logo = Imagen.Leer(logoDataUrl);
        if (logo is not null)
        {
            var (w, h) = Encajar(logo.Ancho, logo.Alto, 230, 66);
            p.Dibujar(logo, Centro - w / 2, y, w, h);
            y += h + 6;
        }

        // Emisor (15px, mayúsculas, espaciado 2px).
        p.LineaSerif((issuerName ?? "").ToUpperInvariant(), Centro, y, Fuente.Serif, 15, Gris700, 2);
        y += 15 * SerifLinea + 14;
        // «Certificado de aprobación» (13px, espaciado 5px, acento).
        p.LineaSerif("CERTIFICADO DE APROBACIÓN", Centro, y, Fuente.Serif, 13, acento, 5);
        y += 13 * SerifLinea + 2;
        // Título (40px negrita, acento).
        p.LineaSerif("Constancia de cumplimiento", Centro, y, Fuente.SerifNegrita, 40, acento, 1, AnchoCuerpo);
        y += 40 * SerifLinea + 6;
        p.LineaSerif("Se certifica que", Centro, y, Fuente.Serif, 15, Gris600);
        y += 15 * SerifLinea + 14;

        // Nombre en cursiva (34px) con línea de 2px debajo, 20px más ancha a cada lado.
        var nombre = learnerName ?? "";
        double tamNombre = Math.Min(34, 34 * (AnchoCuerpo - 40)
            / Math.Max(1, Lienzo.Ancho(nombre, Fuente.SerifCursiva, 34) * FactorGeorgia(Fuente.SerifCursiva)));
        p.LineaSerif(nombre, Centro, y + (34 - tamNombre) * SerifLinea / 2, Fuente.SerifCursiva, tamNombre, Tinta);
        double anchoNombre = Lienzo.Ancho(nombre, Fuente.SerifCursiva, tamNombre) + 40;
        y += 34 * SerifLinea + 6;
        p.Relleno(Centro - anchoNombre / 2, y, anchoNombre, 2, Gris300);
        y += 2 + 8;

        p.LineaSerif("ha completado y aprobado satisfactoriamente el adiestramiento", Centro, y, Fuente.Serif, 15, Gris600);
        y += 15 * SerifLinea + 6;

        // Curso (22px negrita), en varias líneas si no cabe.
        foreach (var linea in Lienzo.Partir(trainingTitle ?? "", Fuente.SerifNegrita, 22, AnchoCuerpo, 3))
        {
            p.LineaSerif(linea, Centro, y, Fuente.SerifNegrita, 22, Tinta);
            y += 22 * SerifLinea;
        }
        y += 6;

        // Leyenda (14px, interlineado 21px, ancho máximo 640).
        if (!string.IsNullOrWhiteSpace(statement))
        {
            y += 8;
            foreach (var linea in Lienzo.Partir(statement!.Trim(), Fuente.Serif, 14, 640, 5))
            {
                p.Texto(linea, Centro, y + (21 - 14 * SerifLinea) / 2 + 14 * SerifBase, Fuente.Serif, 14, Gris600);
                y += 21;
            }
        }

        // --- Fila de datos: etiqueta 11px en mayúsculas y valor 15px negrita, columnas a 26px ---
        y += 14;
        var columnas = new List<(string etiqueta, string valor, (double r, double g, double b) color)>
        {
            ("FECHA DE EMISIÓN", Fecha(issuedAt), Gris700)
        };
        if (showScore) columnas.Add(("CALIFICACIÓN", $"{scorePercent}%", Gris700));
        if (anulado) columnas.Add(("ESTADO", "Anulado", Anulado));
        else if (reemplazado) columnas.Add(("ESTADO", "Reemplazado", Gris500));
        else if (showValidity)
        {
            if (expiresAt is not DateTime vence) columnas.Add(("VIGENCIA", "Sin caducidad", Gris700));
            else if (DateTime.UtcNow >= vence) columnas.Add(("VENCIÓ", Fecha(vence), Vencido));
            else columnas.Add(("VÁLIDO HASTA", Fecha(vence), Vigente));
        }
        var anchos = columnas.Select(c => Math.Max(Lienzo.Ancho(c.etiqueta, Fuente.Sans, 11, 1),
                                                   Lienzo.Ancho(c.valor, Fuente.SansNegrita, 15))).ToList();
        double x = Centro - (anchos.Sum() + 26 * (columnas.Count - 1)) / 2;
        for (int i = 0; i < columnas.Count; i++)
        {
            double cx = x + anchos[i] / 2;
            p.Texto(columnas[i].etiqueta, cx, y + 11 * SansBase, Fuente.Sans, 11, Gris400, 1);
            p.Texto(columnas[i].valor, cx, y + 11 * SansLinea + 15 * SansBase, Fuente.SansNegrita, 15, columnas[i].color);
            x += anchos[i] + 26;
        }

        // --- Pie: firma a la izquierda y sello a la derecha, apoyados en y = 597 ---
        double pie = AltoPx - 44 - 66;
        var firma = Imagen.Leer(signatureDataUrl);
        bool hayFirma = !string.IsNullOrWhiteSpace(signatoryName) || !string.IsNullOrWhiteSpace(signatoryTitle) || firma is not null;
        string nombreFirma = hayFirma ? (signatoryName ?? "").Trim() : "";
        string cargo = hayFirma ? (signatoryTitle ?? "").Trim() : "Firma autorizada";
        double anchoFirma = Math.Max(220, Math.Max(Lienzo.Ancho(nombreFirma, Fuente.Serif, 14) * FactorGeorgia(Fuente.Serif), Lienzo.Ancho(cargo, Fuente.Sans, 12)));
        (double w, double h) tamFirma = firma is null ? (0, 0) : Encajar(firma.Ancho, firma.Alto, 200, 56);
        anchoFirma = Math.Max(anchoFirma, tamFirma.w);
        double cxFirma = 84 + anchoFirma / 2;
        double yCargo = pie - 12 * SansLinea;                       // .role
        double yLinea = yCargo - (1.5 + 6 + 14 * SerifLinea);       // borde superior de .line
        if (firma is not null)
            p.Dibujar(firma, cxFirma - tamFirma.w / 2, yLinea + 4 - tamFirma.h, tamFirma.w, tamFirma.h);
        p.Relleno(84, yLinea, anchoFirma, 1.5, Gris500);
        p.Texto(nombreFirma, cxFirma, yLinea + 1.5 + 6 + 14 * SerifBase, Fuente.Serif, 14, Tinta);
        p.Texto(cargo, cxFirma, yCargo + 12 * SansBase, Fuente.Sans, 12, Gris500);

        // Sello: círculo de 96px con borde doble, girado -8°, opacidad .9.
        p.Sello(916 - 48, pie - 48, Mezcla(acento, 0.9), passPercent);

        // --- Folio y plataforma (.serial: 11px / 10px, interlineado 16.5, a 34px del pie) ---
        double ySerial = AltoPx - 34 - 33;
        double baseSerial = (16.5 - 11 * SansLinea) / 2 + 11 * SansBase;
        p.Texto($"Folio: {serial} · Verificable con el emisor", Centro, ySerial + baseSerial, Fuente.Sans, 11, Gris400, 1);
        p.Texto("Administrado a través de Aprendor", Centro, ySerial + 16.5 + baseSerial, Fuente.Sans, 10, Plataforma, 0.5);

        return Armar(p);
    }

    // Tamaño con el que se ve una imagen con max-width / max-height (sin agrandarla).
    private static (double w, double h) Encajar(int ancho, int alto, double maxAncho, double maxAlto)
    {
        double k = Math.Min(1, Math.Min(maxAncho / ancho, maxAlto / alto));
        return (ancho * k, alto * k);
    }

    // Un color con opacidad sobre fondo blanco.
    private static (double r, double g, double b) Mezcla((double r, double g, double b) c, double opacidad)
        => (c.r * opacidad + 1 - opacidad, c.g * opacidad + 1 - opacidad, c.b * opacidad + 1 - opacidad);

    private static (double r, double g, double b) Hex(string hex) => Color(hex);

    private static (double r, double g, double b) Color(string? hex)
    {
        try
        {
            var h = (hex ?? "#1e3a8a").Trim().TrimStart('#');
            if (h.Length != 6) h = "1e3a8a";
            return (Convert.ToInt32(h[..2], 16) / 255.0,
                    Convert.ToInt32(h.Substring(2, 2), 16) / 255.0,
                    Convert.ToInt32(h.Substring(4, 2), 16) / 255.0);
        }
        catch { return (0.12, 0.23, 0.54); }
    }

    // ------------------------------------------------------------------------
    //  Lienzo: el flujo de contenido de la página, en px de la plantilla
    // ------------------------------------------------------------------------
    private sealed class Lienzo
    {
        public readonly StringBuilder Flujo = new();
        public readonly List<Imagen> Imagenes = new();

        private static string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        private static double X(double px) => px * S;
        private static double Y(double px) => AltoPag - px * S;
        private static string Rgb((double r, double g, double b) c) => $"{N(c.r)} {N(c.g)} {N(c.b)}";

        // Ancho en px de un texto (con el espaciado entre letras de CSS, que Chrome
        // suma también detrás de la última letra).
        public static double Ancho(string texto, Fuente f, double tam, double espaciado = 0)
        {
            var tabla = Anchos[(int)f];
            double total = 0;
            foreach (var b in WinAnsi(texto))
                total += b >= 32 ? tabla[b - 32] : 0;
            return total * tam / 1000.0 + espaciado * texto.Length;
        }

        // Reparte un texto en líneas de ancho máximo (px). La última línea que cabe se
        // cierra con «…» si el texto sigue.
        public static List<string> Partir(string texto, Fuente f, double tam, double anchoMax, int maxLineas)
        {
            var salida = new List<string>();
            var actual = "";
            var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < palabras.Length; i++)
            {
                var prueba = actual.Length == 0 ? palabras[i] : actual + " " + palabras[i];
                if (Ancho(prueba, f, tam) * FactorGeorgia(f) > anchoMax && actual.Length > 0)
                {
                    salida.Add(actual);
                    actual = palabras[i];
                    if (salida.Count == maxLineas)
                    {
                        salida[^1] = salida[^1] + " …";
                        return salida;
                    }
                }
                else actual = prueba;
            }
            if (actual.Length > 0) salida.Add(actual);
            return salida;
        }

        // Una línea serif con line-height normal, desde el borde superior de su caja.
        public void LineaSerif(string texto, double cx, double arriba, Fuente f, double tam,
            (double r, double g, double b) color, double espaciado = 0, double anchoMax = 0)
        {
            // Si no cabe (títulos muy largos), se achica la letra en vez de salirse del marco.
            if (anchoMax > 0)
            {
                var a = Ancho(texto, f, tam) * FactorGeorgia(f) + espaciado * texto.Length;
                if (a > anchoMax) { var k = anchoMax / a; tam *= k; espaciado *= k; }
            }
            Texto(texto, cx, arriba + tam * SerifBase, f, tam, color, espaciado);
        }

        // Texto centrado en cx con su línea base en y (px).
        public void Texto(string texto, double cx, double linaBase, Fuente f, double tam,
            (double r, double g, double b) color, double espaciado = 0)
        {
            if (string.IsNullOrEmpty(texto)) return;
            double x0 = cx - Ancho(texto, f, tam, espaciado) / 2;
            Flujo.Append($"BT {Rgb(color)} rg /F{(int)f + 1} {N(tam * S)} Tf {N(espaciado * S)} Tc " +
                         $"{N(X(x0))} {N(Y(linaBase))} Td ({Cadena(texto, f)}) Tj ET\n");
        }

        // Marca de agua: texto grande girado `grados` (en sentido antihorario, sube hacia la
        // derecha) y centrado en (cx, cy) px. Se dibuja primero para que quede detrás.
        public void MarcaAgua(string texto, double cx, double cy, Fuente f, double tam,
            (double r, double g, double b) color, double grados)
        {
            double a = grados * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            double w = Ancho(texto, f, tam, 8);
            Flujo.Append($"q {N(c)} {N(s)} {N(-s)} {N(c)} {N(X(cx))} {N(Y(cy))} cm\n");
            // Alto de las mayúsculas de Helvetica: ~0.72 del cuerpo; se centra en vertical.
            Flujo.Append($"BT {Rgb(color)} rg /F{(int)f + 1} {N(tam * S)} Tf {N(8 * S)} Tc " +
                         $"{N(-w / 2 * S)} {N(-0.36 * tam * S)} Td ({Cadena(texto, f)}) Tj ET\nQ\n");
        }

        public void Rect(double x, double y, double w, double h, double grosor, (double r, double g, double b) color)
            => Flujo.Append($"{Rgb(color)} RG {N(grosor * S)} w {N(X(x))} {N(Y(y + h))} {N(w * S)} {N(h * S)} re S\n");

        public void Relleno(double x, double y, double w, double h, (double r, double g, double b) color)
            => Flujo.Append($"{Rgb(color)} rg {N(X(x))} {N(Y(y + h))} {N(w * S)} {N(h * S)} re f\n");

        public void Dibujar(Imagen img, double x, double y, double w, double h)
        {
            if (!Imagenes.Contains(img)) Imagenes.Add(img);
            var nombre = "Im" + (Imagenes.IndexOf(img) + 1);
            Flujo.Append($"q {N(w * S)} 0 0 {N(h * S)} {N(X(x))} {N(Y(y + h))} cm /{nombre} Do Q\n");
        }

        // Sello .seal: 96px, border 3px double (dos aros de 1px), girado -8° y con
        // «APROBADO», «✓» y «NN% mín.» en columna.
        public void Sello(double cx, double cy, (double r, double g, double b) color, int minimo)
        {
            const double Angulo = 8 * Math.PI / 180;  // -8° en pantalla (y hacia abajo) = +8° en PDF
            double c = Math.Cos(Angulo), s = Math.Sin(Angulo);
            Flujo.Append($"q {N(c)} {N(s)} {N(-s)} {N(c)} {N(X(cx))} {N(Y(cy))} cm\n");
            // Desde aquí, coordenadas en puntos relativas al centro del sello (y hacia arriba).
            Circulo(47.5 * S, S, color);
            Circulo(45.5 * S, S, color);
            // Contenido: tres líneas centradas (10px / 20px negrita / 10px) en los 90px interiores.
            double altoCol = 10 * SansLinea + 20 * SansLinea + 10 * SansLinea;
            double arriba = -altoCol / 2;
            void Linea(string t, Fuente f, double tam, double espaciado, double baseDesdeCentro)
            {
                double w = Ancho(t, f, tam, espaciado);
                Flujo.Append($"BT {Rgb(color)} rg /F{(int)f + 1} {N(tam * S)} Tf {N(espaciado * S)} Tc " +
                             $"{N(-w / 2 * S)} {N(-baseDesdeCentro * S)} Td ({Cadena(t, f)}) Tj ET\n");
            }
            Linea("APROBADO", Fuente.Sans, 10, 2, arriba + 10 * SansBase);
            // «✓» dibujado como trazo y no con la fuente ZapfDingbats: hay visores (poppler
            // sin fuentes base, algunos móviles) que no la traen y dejarían el sello sin marca.
            double medio = arriba + 10 * SansLinea + 20 * SansLinea / 2;   // centro de la línea de 20px
            Flujo.Append($"{Rgb(color)} RG {N(2.2 * S)} w 1 J 1 j " +
                         $"{N(-5 * S)} {N(-(medio - 0.5) * S)} m {N(-1.5 * S)} {N(-(medio + 3.5) * S)} l " +
                         $"{N(5.5 * S)} {N(-(medio - 5.5) * S)} l S\n");
            Linea($"{minimo}% mín.", Fuente.Sans, 10, 2, arriba + 10 * SansLinea + 20 * SansLinea + 10 * SansBase);
            Flujo.Append("Q\n");
        }

        // Círculo con cuatro curvas de Bézier, centrado en el origen actual (puntos).
        private void Circulo(double r, double grosor, (double r, double g, double b) color)
        {
            double k = 0.5523 * r;
            Flujo.Append($"{Rgb(color)} RG {N(grosor)} w {N(r)} 0 m " +
                         $"{N(r)} {N(k)} {N(k)} {N(r)} 0 {N(r)} c " +
                         $"{N(-k)} {N(r)} {N(-r)} {N(k)} {N(-r)} 0 c " +
                         $"{N(-r)} {N(-k)} {N(-k)} {N(-r)} 0 {N(-r)} c " +
                         $"{N(k)} {N(-r)} {N(r)} {N(-k)} {N(r)} 0 c S\n");
        }

        // Cadena literal de PDF. El flujo se escribe en Latin-1, así que cada carácter de
        // aquí es un byte: los de WinAnsi fuera de ASCII van en octal.
        private static string Cadena(string texto, Fuente f)
        {
            var sb = new StringBuilder();
            foreach (var b in WinAnsi(texto))
            {
                if (b is (byte)'(' or (byte)')' or (byte)'\\') sb.Append('\\').Append((char)b);
                else if (b < 32 || b > 126) sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                else sb.Append((char)b);
            }
            return sb.ToString();
        }
    }

    // Texto a WinAnsi (Windows-1252), la codificación de las fuentes estándar. Sin tablas
    // de códigos del sistema: lo que no existe en WinAnsi sale como «?».
    private static readonly Dictionary<char, byte> WinAnsiAlto = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87,
        ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91,
        ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98,
        ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B, ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static byte[] WinAnsi(string texto)
    {
        var s = (texto ?? "").Normalize(NormalizationForm.FormC);
        var salida = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            salida[i] = c switch
            {
                >= ' ' and <= '~' => (byte)c,
                >= ' ' and <= 'ÿ' => (byte)c,
                _ when WinAnsiAlto.TryGetValue(c, out var b) => b,
                '\t' or '\n' or '\r' => (byte)' ',
                _ => (byte)'?'
            };
        }
        return salida;
    }

    // ------------------------------------------------------------------------
    //  Imágenes (logo y firma) como XObject: JPEG tal cual, PNG decodificado
    // ------------------------------------------------------------------------
    private sealed class Imagen
    {
        public int Ancho, Alto;
        public string Filtro = "";            // /DCTDecode o /FlateDecode
        public string Espacio = "/DeviceRGB";
        public string Extra = "";             // p. ej. /Decode para JPEG CMYK
        public byte[] Datos = Array.Empty<byte>();
        public byte[]? Alfa;                  // canal alfa comprimido (SMask), si lo hay

        private static readonly System.Text.RegularExpressions.Regex DataUrl =
            new(@"^data:image/(png|jpeg);base64,([A-Za-z0-9+/]+={0,2})$", System.Text.RegularExpressions.RegexOptions.Compiled);

        // null si no hay imagen, no es PNG/JPEG o no se pudo leer: el certificado sale sin ella.
        public static Imagen? Leer(string? dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl)) return null;
            var m = DataUrl.Match(dataUrl);
            if (!m.Success) return null;
            try
            {
                var bytes = Convert.FromBase64String(m.Groups[2].Value);
                // Se mira la firma del archivo, no solo el tipo declarado.
                if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P') return Png(bytes);
                if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8) return Jpeg(bytes);
                return null;
            }
            catch { return null; }
        }

        private static Imagen? Jpeg(byte[] b)
        {
            int i = 2;
            while (i + 9 < b.Length)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marca = b[i + 1];
                if (marca is 0xD8 or 0x01 || (marca >= 0xD0 && marca <= 0xD7)) { i += 2; continue; }
                int largo = (b[i + 2] << 8) | b[i + 3];
                // SOF0..SOF15 salvo DHT (C4), JPG (C8) y DAC (CC).
                if (marca >= 0xC0 && marca <= 0xCF && marca is not (0xC4 or 0xC8 or 0xCC))
                {
                    int alto = (b[i + 5] << 8) | b[i + 6], ancho = (b[i + 7] << 8) | b[i + 8], comp = b[i + 9];
                    if (ancho <= 0 || alto <= 0) return null;
                    var img = new Imagen { Ancho = ancho, Alto = alto, Filtro = "/DCTDecode", Datos = b };
                    if (comp == 1) img.Espacio = "/DeviceGray";
                    else if (comp == 4) { img.Espacio = "/DeviceCMYK"; img.Extra = " /Decode [1 0 1 0 1 0 1 0]"; }
                    else if (comp != 3) return null;
                    return img;
                }
                i += 2 + largo;
            }
            return null;
        }

        private static Imagen? Png(byte[] b)
        {
            int pos = 8, ancho = 0, alto = 0, bits = 0, tipo = 0, entrelazado = 0;
            byte[]? paleta = null, trns = null;
            var idat = new MemoryStream();
            while (pos + 8 <= b.Length)
            {
                int largo = (b[pos] << 24) | (b[pos + 1] << 16) | (b[pos + 2] << 8) | b[pos + 3];
                var nombre = Encoding.ASCII.GetString(b, pos + 4, 4);
                if (largo < 0 || pos + 12 + largo > b.Length) return null;
                var datos = new ReadOnlySpan<byte>(b, pos + 8, largo);
                switch (nombre)
                {
                    case "IHDR":
                        ancho = (datos[0] << 24) | (datos[1] << 16) | (datos[2] << 8) | datos[3];
                        alto = (datos[4] << 24) | (datos[5] << 16) | (datos[6] << 8) | datos[7];
                        bits = datos[8]; tipo = datos[9]; entrelazado = datos[12];
                        break;
                    case "PLTE": paleta = datos.ToArray(); break;
                    case "tRNS": trns = datos.ToArray(); break;
                    case "IDAT": idat.Write(datos); break;
                }
                if (nombre == "IEND") break;
                pos += 12 + largo;
            }
            // Límite defensivo (el logo ya viene limitado a ~1 MB en base64).
            if (ancho <= 0 || alto <= 0 || (long)ancho * alto > 16_000_000 || entrelazado != 0) return null;

            int canales = tipo switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
            if (canales == 0 || bits is not (1 or 2 or 4 or 8 or 16)) return null;
            if (tipo == 3 && paleta is null) return null;

            byte[] crudo;
            idat.Position = 0;
            using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            using (var ms = new MemoryStream())
            {
                z.CopyTo(ms);
                crudo = ms.ToArray();
            }

            int bitsPixel = canales * bits;
            int bpp = Math.Max(1, bitsPixel / 8);
            int bytesFila = (ancho * bitsPixel + 7) / 8;
            if (crudo.Length < (long)(bytesFila + 1) * alto) return null;

            var rgb = new byte[ancho * alto * 3];
            byte[]? alfa = null;
            if (tipo is 4 or 6 || trns is not null) alfa = new byte[ancho * alto];

            var previa = new byte[bytesFila];
            var fila = new byte[bytesFila];
            for (int yy = 0; yy < alto; yy++)
            {
                int o = yy * (bytesFila + 1);
                int filtro = crudo[o];
                Buffer.BlockCopy(crudo, o + 1, fila, 0, bytesFila);
                for (int i = 0; i < bytesFila; i++)
                {
                    int a = i >= bpp ? fila[i - bpp] : 0, up = previa[i], c = i >= bpp ? previa[i - bpp] : 0;
                    fila[i] = filtro switch
                    {
                        1 => (byte)(fila[i] + a),
                        2 => (byte)(fila[i] + up),
                        3 => (byte)(fila[i] + ((a + up) >> 1)),
                        4 => (byte)(fila[i] + Paeth(a, up, c)),
                        _ => fila[i]
                    };
                }
                for (int xx = 0; xx < ancho; xx++)
                {
                    int d = yy * ancho + xx;
                    int Muestra(int canal)
                    {
                        if (bits == 8) return fila[xx * canales + canal];
                        if (bits == 16) return fila[(xx * canales + canal) * 2];
                        int bit = xx * bits;  // bits < 8: un solo canal (gris o paleta)
                        int v = (fila[bit >> 3] >> (8 - bits - (bit & 7))) & ((1 << bits) - 1);
                        return v;
                    }
                    int r, g, bl, al = 255;
                    switch (tipo)
                    {
                        case 3:
                            int idx = Muestra(0);
                            if (idx * 3 + 2 >= paleta!.Length) { r = g = bl = 0; }
                            else { r = paleta[idx * 3]; g = paleta[idx * 3 + 1]; bl = paleta[idx * 3 + 2]; }
                            if (trns is not null && idx < trns.Length) al = trns[idx];
                            break;
                        case 0:
                        {
                            int v = Muestra(0);
                            if (trns is { Length: >= 2 })
                            {
                                int clave = (trns[0] << 8) | trns[1];
                                int bruto = bits == 16 ? (fila[xx * 2] << 8) | fila[xx * 2 + 1] : v;
                                if (bruto == clave) al = 0;
                            }
                            if (bits < 8) v = v * 255 / ((1 << bits) - 1);
                            r = g = bl = v;
                            break;
                        }
                        case 4: r = g = bl = Muestra(0); al = Muestra(1); break;
                        case 2:
                            r = Muestra(0); g = Muestra(1); bl = Muestra(2);
                            if (trns is { Length: >= 6 } && bits == 8
                                && r == trns[1] && g == trns[3] && bl == trns[5]) al = 0;
                            break;
                        default: r = Muestra(0); g = Muestra(1); bl = Muestra(2); al = Muestra(3); break;
                    }
                    rgb[d * 3] = (byte)r; rgb[d * 3 + 1] = (byte)g; rgb[d * 3 + 2] = (byte)bl;
                    if (alfa is not null) alfa[d] = (byte)al;
                }
                (previa, fila) = (fila, previa);
            }

            return new Imagen
            {
                Ancho = ancho, Alto = alto, Filtro = "/FlateDecode",
                Datos = Comprimir(rgb),
                Alfa = alfa is null || alfa.All(v => v == 255) ? null : Comprimir(alfa)
            };
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static byte[] Comprimir(byte[] datos)
        {
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal)) z.Write(datos);
            return ms.ToArray();
        }
    }

    // ------------------------------------------------------------------------
    //  Ensamblado: objetos, tabla de referencias cruzadas y tráiler
    // ------------------------------------------------------------------------
    private static byte[] Armar(Lienzo p)
    {
        var latin1 = Encoding.Latin1;
        var objetos = new List<byte[]>();
        int Agregar(byte[] o) { objetos.Add(o); return objetos.Count; }
        byte[] Stream(string dic, byte[] datos)
            => Concat(latin1.GetBytes($"<< {dic} /Length {datos.Length} >>\nstream\n"), datos, latin1.GetBytes("\nendstream"));

        // 1 catálogo, 2 páginas, 3 página, 4 contenido; luego fuentes e imágenes.
        Agregar(latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        Agregar(latin1.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"));
        Agregar(Array.Empty<byte>());   // la página se escribe al final, cuando se conocen los recursos
        Agregar(Stream("", latin1.GetBytes(p.Flujo.ToString())));

        var fuentes = new StringBuilder();
        for (int i = 0; i < NombresFuente.Length; i++)
        {
            int n = Agregar(latin1.GetBytes($"<< /Type /Font /Subtype /Type1 /BaseFont /{NombresFuente[i]} /Encoding /WinAnsiEncoding >>"));
            fuentes.Append($"/F{i + 1} {n} 0 R ");
        }

        var xobjetos = new StringBuilder();
        for (int i = 0; i < p.Imagenes.Count; i++)
        {
            var img = p.Imagenes[i];
            var mascara = "";
            if (img.Alfa is not null)
            {
                int m = Agregar(Stream($"/Type /XObject /Subtype /Image /Width {img.Ancho} /Height {img.Alto} " +
                                       "/ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode", img.Alfa));
                mascara = $" /SMask {m} 0 R";
            }
            int n = Agregar(Stream($"/Type /XObject /Subtype /Image /Width {img.Ancho} /Height {img.Alto} " +
                                   $"/ColorSpace {img.Espacio} /BitsPerComponent 8 /Filter {img.Filtro}{img.Extra}{mascara}", img.Datos));
            xobjetos.Append($"/Im{i + 1} {n} 0 R ");
        }

        var recursos = $"/Font << {fuentes}>>" + (xobjetos.Length > 0 ? $" /XObject << {xobjetos}>>" : "");
        objetos[2] = latin1.GetBytes(
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {AnchoPag.ToString("0.##", CultureInfo.InvariantCulture)} " +
            $"{AltoPag.ToString("0.##", CultureInfo.InvariantCulture)}] /Resources << {recursos} >> /Contents 4 0 R >>");

        var salida = new MemoryStream();
        void Escribir(byte[] b) => salida.Write(b, 0, b.Length);

        Escribir(latin1.GetBytes("%PDF-1.4\n%âãÏÓ\n"));
        var posiciones = new List<long>();
        for (int i = 0; i < objetos.Count; i++)
        {
            posiciones.Add(salida.Position);
            Escribir(latin1.GetBytes($"{i + 1} 0 obj\n"));
            Escribir(objetos[i]);
            Escribir(latin1.GetBytes("\nendobj\n"));
        }

        var inicioXref = salida.Position;
        Escribir(latin1.GetBytes($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n"));
        foreach (var pos in posiciones) Escribir(latin1.GetBytes($"{pos:0000000000} 00000 n \n"));
        Escribir(latin1.GetBytes($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF"));

        return salida.ToArray();
    }

    private static byte[] Concat(params byte[][] partes)
    {
        var total = partes.Sum(p => p.Length);
        var salida = new byte[total];
        int o = 0;
        foreach (var p in partes) { Buffer.BlockCopy(p, 0, salida, o, p.Length); o += p.Length; }
        return salida;
    }
}
