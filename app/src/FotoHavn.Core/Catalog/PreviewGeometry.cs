namespace FotoHavn.Core.Catalog;

/// <summary>The preview-only rotation of landscape Templates, a pure port of the website's <c>previewLayout</c>.</summary>
public static class PreviewGeometry
{
    /// <summary>
    /// The Template as the UI previews it. A portrait or square Template is returned unchanged. A landscape Template turns a
    /// quarter turn counterclockwise: width and height swap, and the original leftmost window becomes the bottom window. The
    /// saved PNG always keeps its native orientation.
    /// </summary>
    public static TemplateDefinition Rotate(TemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!template.IsLandscape)
        {
            return template;
        }

        return template with
        {
            Width = template.Height,
            Height = template.Width,
            Windows = [.. template.Windows.Select(window => new WindowRect(
                window.Y,
                template.Width - window.X - window.Width,
                window.Height,
                window.Width))],
        };
    }
}
