using System.Text;

namespace TrainingPlatform.Certificates;

// Generador de PDF del certificado, escrito a mano.
//
// Por qué a mano y no con una librería: el PDF va adjunto en un correo que sale
// solo, en servidores que no controlamos (IIS en casa del cliente, contenedor
// Linux, lo que toque). Las librerías de PDF arrastran fuentes y dependencias
// nativas que fallan distinto en cada sitio. Aquí se usan las fuentes estándar
// del propio formato (Helvetica), que todo lector trae: cero archivos, cero
// dependencias, mismo resultado en cualquier lado.
//
// Limitación consciente: no se incrustan el logo ni la firma escaneada. El
// certificado en pantalla (certificate.html) sí los muestra; el PDF es el
// documento de archivo y va en texto, con el mismo contenido y folio.
public static class CertificatePdf
{
    // Anchos de Helvetica en milésimas de punto, ASCII 32..126. Hacen falta para
    // centrar el texto: sin ellos no se puede saber cuánto mide una línea.
    private static readonly int[] W = {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,
        556,556,278,278,584,584,584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,333,556,556,500,556,556,278,556,
        556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584 };

    private static readonly int[] WB = {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,
        556,556,333,333,584,584,584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,333,556,611,556,611,556,333,611,
        611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584 };

    private static double Ancho(string texto, double tam, bool negrita)
    {
        var tabla = negrita ? WB : W;
        double total = 0;
        foreach (var c in texto)
        {
            var i = c - 32;
            total += (i >= 0 && i < tabla.Length) ? tabla[i] : (negrita ? 611 : 556);  // acentos: ancho medio
        }
        return total * tam / 1000.0;
    }

    // Los textos van en WinAnsi (Windows-1252) porque es la codificación de las
    // fuentes estándar; así los acentos y la ñ salen bien.
    private static string Escapar(string s)
        => (s ?? "").Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    public static byte[] Render(
        string issuerName, string learnerName, string trainingTitle, string serial,
        DateTime issuedAt, DateTime? expiresAt, int scorePercent, int passPercent,
        bool showScore, bool showValidity, string? statement,
        string? signatoryName, string? signatoryTitle, string accentColor)
    {
        // Carta apaisada, en puntos.
        const double AnchoPag = 792, AltoPag = 612;
        var (ar, ag, ab) = Color(accentColor);
        var cuerpo = new StringBuilder();

        // --- Marco ---
        cuerpo.Append($"{ar:0.###} {ag:0.###} {ab:0.###} RG 3 w 28 28 {AnchoPag - 56:0.##} {AltoPag - 56:0.##} re S\n");
        cuerpo.Append($"0.6 w 40 40 {AnchoPag - 80:0.##} {AltoPag - 80:0.##} re S\n");

        void Centrado(string texto, double y, double tam, bool negrita, (double r, double g, double b)? color = null)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            var c = color ?? (0.06, 0.09, 0.16);
            var x = (AnchoPag - Ancho(texto, tam, negrita)) / 2;
            cuerpo.Append($"BT {c.r:0.###} {c.g:0.###} {c.b:0.###} rg /{(negrita ? "F2" : "F1")} {tam:0.##} Tf " +
                          $"{x:0.##} {y:0.##} Td ({Escapar(texto)}) Tj ET\n");
        }

        void Linea(double x1, double y1, double x2, double y2)
            => cuerpo.Append($"0.55 0.58 0.64 RG 0.8 w {x1:0.##} {y1:0.##} m {x2:0.##} {y2:0.##} l S\n");

        // --- Contenido ---
        Centrado(issuerName?.ToUpperInvariant() ?? "", AltoPag - 110, 13, true, (ar, ag, ab));
        // Filete de acento bajo el emisor, en vez de una banda que compita con el marco.
        cuerpo.Append($"{ar:0.###} {ag:0.###} {ab:0.###} rg " +
                      $"{AnchoPag / 2 - 45:0.##} {AltoPag - 126:0.##} 90 2.5 re f\n");
        Centrado("CERTIFICADO DE APROBACIÓN", AltoPag - 176, 27, true);
        Centrado("Se certifica que", AltoPag - 214, 12, false, (0.4, 0.45, 0.5));
        Centrado(learnerName ?? "", AltoPag - 258, 30, true);
        Linea(AnchoPag / 2 - 170, AltoPag - 270, AnchoPag / 2 + 170, AltoPag - 270);
        Centrado("completó satisfactoriamente el adiestramiento", AltoPag - 296, 12, false, (0.4, 0.45, 0.5));

        // El título puede ser largo: se parte en dos líneas si no cabe.
        var titulo = trainingTitle ?? "";
        double yTitulo = AltoPag - 330;
        if (Ancho(titulo, 18, true) > AnchoPag - 200)
        {
            var corte = titulo.LastIndexOf(' ', Math.Min(titulo.Length - 1, titulo.Length / 2 + 12));
            if (corte > 0)
            {
                Centrado(titulo[..corte], yTitulo, 18, true);
                Centrado(titulo[(corte + 1)..], yTitulo - 24, 18, true);
                yTitulo -= 24;
            }
            else Centrado(titulo, yTitulo, 18, true);
        }
        else Centrado(titulo, yTitulo, 18, true);

        double y = yTitulo - 42;
        if (showScore)
        {
            Centrado($"Puntuación obtenida: {scorePercent}%   ·   Mínimo requerido: {passPercent}%", y, 11.5, false, (0.28, 0.33, 0.4));
            y -= 22;
        }

        var emitido = $"Emitido el {issuedAt.ToLocalTime():dd/MM/yyyy}";
        if (showValidity && expiresAt is DateTime v) emitido += $"   ·   Vigente hasta el {v.ToLocalTime():dd/MM/yyyy}";
        Centrado(emitido, y, 11.5, false, (0.28, 0.33, 0.4));
        y -= 26;

        if (!string.IsNullOrWhiteSpace(statement))
        {
            // Leyenda legal: se reparte en líneas de ancho máximo.
            foreach (var linea in Partir(statement!, 10, false, AnchoPag - 220))
            {
                Centrado(linea, y, 10, false, (0.42, 0.47, 0.54));
                y -= 14;
            }
        }

        // --- Firma ---
        double yFirma = 118;
        if (!string.IsNullOrWhiteSpace(signatoryName))
        {
            Linea(AnchoPag / 2 - 120, yFirma, AnchoPag / 2 + 120, yFirma);
            Centrado(signatoryName!, yFirma - 18, 12, true);
            if (!string.IsNullOrWhiteSpace(signatoryTitle))
                Centrado(signatoryTitle!, yFirma - 34, 10, false, (0.42, 0.47, 0.54));
        }

        Centrado($"Folio: {serial}   ·   Verificable con el emisor", 62, 9.5, false, (0.5, 0.55, 0.62));

        return Armar(cuerpo.ToString(), AnchoPag, AltoPag);
    }

    private static List<string> Partir(string texto, double tam, bool negrita, double anchoMax)
    {
        var salida = new List<string>();
        var actual = new StringBuilder();
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var prueba = actual.Length == 0 ? palabra : actual + " " + palabra;
            if (Ancho(prueba, tam, negrita) > anchoMax && actual.Length > 0)
            {
                salida.Add(actual.ToString());
                actual.Clear().Append(palabra);
            }
            else { actual.Clear().Append(prueba); }
            if (salida.Count >= 4) break;   // no dejar que una leyenda enorme desborde la hoja
        }
        if (actual.Length > 0 && salida.Count < 5) salida.Add(actual.ToString());
        return salida;
    }

    private static (double r, double g, double b) Color(string? hex)
    {
        try
        {
            var h = (hex ?? "#1e3a8a").TrimStart('#');
            if (h.Length != 6) h = "1e3a8a";
            return (Convert.ToInt32(h[..2], 16) / 255.0,
                    Convert.ToInt32(h.Substring(2, 2), 16) / 255.0,
                    Convert.ToInt32(h.Substring(4, 2), 16) / 255.0);
        }
        catch { return (0.12, 0.23, 0.54); }
    }

    // Ensambla el archivo: objetos, tabla de referencias cruzadas y tráiler.
    private static byte[] Armar(string contenido, double ancho, double alto)
    {
        var win = Encoding.GetEncoding(1252);
        var flujo = win.GetBytes(contenido);

        var objetos = new List<byte[]>
        {
            win.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            win.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            win.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {ancho:0.##} {alto:0.##}] " +
                         "/Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>"),
            Concat(win.GetBytes($"<< /Length {flujo.Length} >>\nstream\n"), flujo, win.GetBytes("\nendstream")),
            win.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            win.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
        };

        var salida = new MemoryStream();
        void Escribir(byte[] b) => salida.Write(b, 0, b.Length);

        Escribir(win.GetBytes("%PDF-1.4\n%âãÏÓ\n"));
        var posiciones = new List<long>();
        for (int i = 0; i < objetos.Count; i++)
        {
            posiciones.Add(salida.Position);
            Escribir(win.GetBytes($"{i + 1} 0 obj\n"));
            Escribir(objetos[i]);
            Escribir(win.GetBytes("\nendobj\n"));
        }

        var inicioXref = salida.Position;
        Escribir(win.GetBytes($"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n"));
        foreach (var p in posiciones) Escribir(win.GetBytes($"{p:0000000000} 00000 n \n"));
        Escribir(win.GetBytes($"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioXref}\n%%EOF"));

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
