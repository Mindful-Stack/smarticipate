using System.Globalization;
using System.Text;
using QRCoder;

namespace Smarticipate.Web.Services;

// Builds a Smarticipate-branded QR code (dark modules on beige, a cleared centre for the logo) as an inline SVG string.
public static class QrCodeSvg
{
    private const string Dark = "#393552";
    private const string Light = "#f2e9e1";

    public static string ForLink(string link, string logoHref = "/images/smarticipate-icon.svg")
    {
        using var generator = new QRCodeGenerator();
        // High ECC tolerates the centre logo covering part of the code.
        using var data = generator.CreateQrCode(link, QRCodeGenerator.ECCLevel.H);
        var matrix = data.ModuleMatrix;
        int size = matrix.Count;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"100%\" height=\"100%\" shape-rendering=\"crispEdges\" style=\"display:block\" role=\"img\" aria-label=\"QR code to join the session\">");
        sb.Append($"<rect width=\"{size}\" height=\"{size}\" fill=\"{Light}\"/>");
        sb.Append($"<path fill=\"{Dark}\" d=\"");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (matrix[y][x])
                    sb.Append($"M{x} {y}h1v1h-1z");
            }
        }
        sb.Append("\"/>");

        double backing = size * 0.28;
        double backingPos = (size - backing) / 2.0;
        double icon = size * 0.21;
        double iconPos = (size - icon) / 2.0;

        sb.Append($"<rect x=\"{Inv(backingPos)}\" y=\"{Inv(backingPos)}\" width=\"{Inv(backing)}\" height=\"{Inv(backing)}\" rx=\"1\" ry=\"1\" fill=\"{Light}\" stroke=\"{Dark}\" stroke-width=\"0.4\"/>");
        sb.Append($"<image href=\"{logoHref}\" x=\"{Inv(iconPos)}\" y=\"{Inv(iconPos)}\" width=\"{Inv(icon)}\" height=\"{Inv(icon)}\"/>");
        sb.Append("</svg>");

        return sb.ToString();
    }

    private static string Inv(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
