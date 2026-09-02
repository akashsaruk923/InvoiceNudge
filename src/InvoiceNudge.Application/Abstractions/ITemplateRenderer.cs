namespace InvoiceNudge.Application.Abstractions;

public interface ITemplateRenderer
{
    /// <summary>Renders a Scriban template against a model. Placeholders use {{ property }} syntax.</summary>
    string Render(string template, object model);
}
